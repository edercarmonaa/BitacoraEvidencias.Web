using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;

namespace BitacoraEvidencias.Migration;

public sealed class MigrationValidationException(string message) : Exception(message);

public static class MigrationRunner
{
    private static readonly string[] Tables = ["UsuariosSistema", "Oficios", "CasosCorreccion", "EvidenciasFoto", "AuditLogs"];

    public static async Task RunAsync(string mode, string sourceFile, string storageRoot, bool production)
    {
        sourceFile = Path.GetFullPath(sourceFile);
        storageRoot = Path.GetFullPath(storageRoot);
        RejectProductionSource(sourceFile);
        RejectProductionSource(storageRoot);
        Require(File.Exists(sourceFile) && Directory.Exists(storageRoot), "Falta la copia SQLite o la copia de evidencias.");
        var options = new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer("Server=localhost;Database=DesignOnly;Integrated Security=true;Encrypt=true").Options;
        await using var modelContext = new SqlServerAppDbContext(options);
        // Use the design model: runtime models omit some relational annotations.
        var model = modelContext.GetServiceModel();
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sourceFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ConnectionString);
        await source.OpenAsync();
        using var sourceTransaction = source.BeginTransaction(deferred: true);
        CheckSqliteIntegrity(source, sourceTransaction);
        var data = ReadSource(source, sourceTransaction, model);
        ValidateProtectedValues(data);
        var evidenceHash = ValidateFiles(data["EvidenciasFoto"], storageRoot);
        Require(data["UsuariosSistema"].Rows.Count > 0, "La copia no contiene usuarios; se cancela la migracion.");
        foreach (var (table, rows) in data)
            Console.WriteLine($"Origen validado: {table}, filas={rows.Rows.Count}, SHA256={Digest(rows)}");
        Console.WriteLine($"Evidencias verificadas; huella del conjunto referenciado: {evidenceHash}");
        if (mode == "preflight") return;

        var connectionString = Environment.GetEnvironmentVariable("BITACORA_MIGRATION_SQL_CONNECTION");
        Require(!string.IsNullOrWhiteSpace(connectionString), "Falta BITACORA_MIGRATION_SQL_CONNECTION en el proceso.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        Require(!builder.IntegratedSecurity && !string.IsNullOrWhiteSpace(builder.UserID), "Se requiere autenticacion SQL externa.");
        Require(builder.Encrypt != SqlConnectionEncryptOption.Optional, "La conexion SQL debe usar cifrado.");
        Require(string.Equals(builder.DataSource, @".\Auditoria", StringComparison.OrdinalIgnoreCase), "La instancia de destino debe ser .\\Auditoria.");
        Require(builder.InitialCatalog == (production ? "BitacoraEvidencias" : "BitacoraEvidencias_Pruebas"),
            "Destino incorrecto: pruebas por defecto; produccion requiere --production.");
        builder.PersistSecurityInfo = false;
        builder.ApplicationName = "BitacoraMigration-0.2.0";
        await using var destination = new SqlConnection(builder.ConnectionString);
        await destination.OpenAsync();
        await using var transaction = (SqlTransaction)await destination.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            using var appLock = new SqlCommand("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'BitacoraMigration', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0; SELECT @r;", destination, transaction);
            Require(Convert.ToInt32(await appLock.ExecuteScalarAsync()) >= 0, "Existe otra migracion en curso.");
            if (mode == "import")
            {
                using var check = new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped=0 AND type IN ('U','V','P','FN','IF','TF','SO');", destination, transaction);
                Require(Convert.ToInt32(await check.ExecuteScalarAsync()) == 0, "La base destino no esta vacia. No se elimina ni sobrescribe nada.");
                foreach (var batch in Regex.Split(modelContext.Database.GenerateCreateScript(), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    using var create = new SqlCommand(batch, destination, transaction) { CommandTimeout = 120 };
                    await create.ExecuteNonQueryAsync();
                }
                foreach (var (table, rows) in data)
                {
                    using var bulk = new SqlBulkCopy(destination, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints, transaction)
                    {
                        DestinationTableName = $"[dbo].[{table}]", BulkCopyTimeout = 120
                    };
                    foreach (DataColumn column in rows.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                    await bulk.WriteToServerAsync(rows);
                    using var reseed = new SqlCommand($"DBCC CHECKIDENT ('dbo.{table}', RESEED, {MaxId(rows).ToString(CultureInfo.InvariantCulture)}) WITH NO_INFOMSGS;", destination, transaction);
                    // Leave an empty table's original identity seed intact.
                    if (rows.Rows.Count > 0) await reseed.ExecuteNonQueryAsync();
                }
                using var history = new SqlCommand("CREATE TABLE [dbo].[__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL PRIMARY KEY, [ProductVersion] nvarchar(32) NOT NULL);", destination, transaction);
                await history.ExecuteNonQueryAsync();
                var migrations = modelContext.Database.GetMigrations().ToArray();
                Require(migrations.Length == 1, "El importador inicial requiere exactamente una migracion SQL Server; actualizarlo antes de futuras versiones.");
                using var stamp = new SqlCommand("INSERT INTO [dbo].[__EFMigrationsHistory] VALUES (@id,N'9.0.0');", destination, transaction);
                stamp.Parameters.AddWithValue("@id", migrations[0]);
                await stamp.ExecuteNonQueryAsync();
            }
            foreach (var (table, expected) in data)
            {
                var actual = expected.Clone();
                using var read = new SqlCommand($"SELECT {string.Join(",", expected.Columns.Cast<DataColumn>().Select(c => $"[{c.ColumnName}]"))} FROM [dbo].[{table}] ORDER BY [Id];", destination, transaction);
                using (var reader = await read.ExecuteReaderAsync()) actual.Load(reader);
                Require(expected.Rows.Count == actual.Rows.Count && Digest(expected) == Digest(actual), $"La comparacion completa fallo en {table}; no se confirma la importacion.");
                Console.WriteLine($"Destino identico: {table}, filas={actual.Rows.Count}");
            }
            // Abort if evidence changed while the transfer was running.
            Require(evidenceHash == ValidateFiles(data["EvidenciasFoto"], storageRoot), "Las evidencias cambiaron durante la transferencia.");
            await transaction.CommitAsync();
            Console.WriteLine(mode == "import" ? "Importacion confirmada: tablas y datos en una unica transaccion. Falta validar la aplicacion antes del cambio productivo." : "Verificacion completa aprobada.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public static object ConvertValue(object value, Type type)
    {
        if (value is DBNull) return DBNull.Value;
        if (type == typeof(string)) return Convert.ToString(value, CultureInfo.InvariantCulture)!;
        if (type == typeof(DateTime)) return DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (type == typeof(bool))
        {
            var number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            Require(number is 0 or 1, "La copia contiene un booleano invalido.");
            return number == 1;
        }
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, DataTable> ReadSource(SqliteConnection source, SqliteTransaction transaction, IModel model)
    {
        using var list = source.CreateCommand();
        list.Transaction = transaction;
        list.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        using (var reader = list.ExecuteReader())
            while (reader.Read())
                Require(Tables.Contains(reader.GetString(0)) || reader.GetString(0) is "__EFMigrationsHistory" or "__EFMigrationsLock", "Hay tablas adicionales en SQLite: revisar antes de migrar para no omitir informacion.");
        var result = new Dictionary<string, DataTable>();
        foreach (var table in Tables)
        {
            var entity = model.GetEntityTypes().Single(e => e.GetTableName() == table);
            var identifier = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var properties = entity.GetProperties().OrderBy(p => p.Name).ToArray();
            var expected = properties.Select(p => p.GetColumnName(identifier)!).ToArray();
            using var columns = source.CreateCommand();
            columns.Transaction = transaction;
            columns.CommandText = $"PRAGMA table_info(\"{table}\");";
            var found = new List<string>();
            using (var reader = columns.ExecuteReader()) while (reader.Read()) found.Add(reader.GetString(1));
            Require(found.Order().SequenceEqual(expected.Order()), $"El esquema SQLite no coincide en {table}. Se detiene sin modificar datos.");
            var data = new DataTable(table) { Locale = CultureInfo.InvariantCulture };
            foreach (var property in properties)
                data.Columns.Add(property.GetColumnName(identifier)!, Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);
            using var command = source.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {string.Join(",", expected.Select(c => $"\"{c}\""))} FROM \"{table}\" ORDER BY \"Id\";";
            using var rows = command.ExecuteReader();
            while (rows.Read())
            {
                var values = new object[properties.Length];
                for (var index = 0; index < properties.Length; index++)
                {
                    var property = properties[index];
                    var raw = rows.GetValue(index);
                    Require(raw is not DBNull || property.IsNullable, $"Hay un valor requerido nulo en {table}.{property.Name}.");
                    values[index] = ConvertValue(raw, data.Columns[index].DataType);
                    if (values[index] is string text && property.GetMaxLength() is int max)
                        Require(text.Length <= max, $"Hay un valor que excede el limite en {table}.{property.Name}. No se trunca.");
                }
                data.Rows.Add(values);
            }
            result.Add(table, data);
        }
        return result;
    }

    private static void ValidateProtectedValues(Dictionary<string, DataTable> data)
    {
        var protection = new SensitiveDataProtectionService(Options.Create(new SensitiveDataProtectionOptions()));
        Require(protection.IsConfigured, "Falta la llave de proteccion actual en el entorno. No se genera ni sustituye.");
        foreach (var table in data.Values)
            foreach (DataRow row in table.Rows)
                foreach (var value in row.ItemArray)
                    if (value is string text && protection.IsProtected(text))
                        try { protection.UnprotectString(text); }
                        catch { throw new MigrationValidationException("La llave no permite descifrar un valor existente. Se cancela."); }
    }

    private static string ValidateFiles(DataTable evidence, string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var protection = new SensitiveDataProtectionService(Options.Create(new SensitiveDataProtectionOptions()));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        foreach (DataRow row in evidence.Rows)
            foreach (var column in new[] { "RutaArchivo", "RutaMiniatura" })
            {
                var relative = (string)row[column];
                var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
                Require(!Path.IsPathRooted(relative) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "Una evidencia apunta fuera del almacenamiento de ensayo.");
                Require(File.Exists(path), "Falta un original o una miniatura referenciada. No se acepta la migracion incompleta.");
                using var stream = File.OpenRead(path);
                var bytes = new byte[16];
                var prefixLength = stream.Read(bytes);
                stream.Position = 0;
                if (protection.IsProtectedPayload(bytes.AsSpan(0, prefixLength)))
                {
                    using var content = new MemoryStream();
                    stream.CopyTo(content);
                    try
                    {
                        var plaintext = protection.UnprotectBytes(content.ToArray());
                        CryptographicOperations.ZeroMemory(plaintext);
                    }
                    catch { throw new MigrationValidationException("No se pudo descifrar una evidencia con la llave actual."); }
                    stream.Position = 0;
                }
                hash.AppendData(Encoding.UTF8.GetBytes(relative + "\0"));
                hash.AppendData(SHA256.HashData(stream));
            }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static string Digest(DataTable table)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (DataRow row in table.Rows)
        {
            using var buffer = new MemoryStream();
            using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                foreach (var value in row.ItemArray)
                {
                    writer.Write(value is not DBNull);
                    if (value is DBNull) continue;
                    writer.Write(value switch
                    {
                        DateTime date => date.Ticks.ToString(CultureInfo.InvariantCulture),
                        bool boolean => boolean ? "1" : "0",
                        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!
                    });
                }
            hash.AppendData(buffer.ToArray());
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static long MaxId(DataTable table) => table.Rows.Cast<DataRow>().Max(r => Convert.ToInt64(r["Id"], CultureInfo.InvariantCulture));

    private static void CheckSqliteIntegrity(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA integrity_check;";
        Require(Equals(command.ExecuteScalar(), "ok"), "La integridad de SQLite no es correcta.");
        command.CommandText = "PRAGMA foreign_key_check;";
        using var reader = command.ExecuteReader();
        Require(!reader.Read(), "SQLite contiene relaciones invalidas.");
    }

    private static void RejectProductionSource(string path)
    {
        var production = Path.GetFullPath(@"C:\BitacoraEvidencias");
        Require(!path.Equals(production, StringComparison.OrdinalIgnoreCase) && !path.StartsWith(production + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "Usa una copia consistente del respaldo fuera de la instalacion productiva.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new MigrationValidationException(message);
    }
}

internal static class ModelExtensions
{
    internal static IModel GetServiceModel(this DbContext context) =>
        Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(context).Model;
}
