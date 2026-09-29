using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BitacoraEvidencias.Web.Services;

public static class ApplicationDataStartup
{
    private const string EfMigrationHistoryTableName = "__EFMigrationsHistory";

    public static void CheckLegacySchemaState(WebApplication app, IConfiguration configuration)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        ValidateSqliteDatabaseFileExists(connectionString, app.Environment.ContentRootPath);

        if (!dbContext.Database.IsSqlite())
        {
            app.Logger.LogInformation("Legacy schema check omitido: el proveedor configurado no es SQLite.");
            return;
        }

        if (HasLegacySchemaWithoutMigrationHistory(dbContext))
        {
            throw new InvalidOperationException(
                "Se detecto una base SQLite legacy sin __EFMigrationsHistory. " +
                "Migra formalmente la base o habilita temporalmente Startup:AllowLegacySchemaWithoutMigrations=true solo durante la ventana de transicion.");
        }

        app.Logger.LogInformation("La base configurada ya tiene historial de migraciones de EF Core o aun no ha sido creada.");
    }

    public static void StampLegacySchemaBaseline(WebApplication app, IConfiguration configuration)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        ValidateSqliteDatabaseFileExists(connectionString, app.Environment.ContentRootPath);

        if (!dbContext.Database.IsSqlite())
        {
            throw new InvalidOperationException(
                "El stamp del baseline solo esta soportado para bases SQLite legacy.");
        }

        if (!HasLegacySchemaWithoutMigrationHistory(dbContext))
        {
            app.Logger.LogInformation(
                "No se requiere stamp del baseline: la base ya tiene historial de migraciones de EF Core o aun no ha sido creada.");
            return;
        }

        ValidateApplicationDataReady(dbContext, connectionString, app.Environment.ContentRootPath);
        StampBaselineMigrationHistory(dbContext);

        app.Logger.LogWarning(
            "Se registro el baseline de migraciones de EF Core sobre una base SQLite legacy. " +
            "Ejecuta despues la inicializacion normal sin Startup:AllowLegacySchemaWithoutMigrations para aplicar futuras migraciones.");
    }

    public static void InitializeApplicationData(
        WebApplication app,
        IConfiguration configuration,
        bool prepareApplicationData,
        bool resetApplicationData)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (prepareApplicationData)
        {
            ValidateStartupConfiguration(app.Environment, resetApplicationData);

            PrepareApplicationData(
                dbContext,
                configuration,
                configuration.GetConnectionString("DefaultConnection"),
                app.Environment.ContentRootPath,
                app.Environment,
                app.Logger,
                resetApplicationData);
        }
        else
        {
            ValidateApplicationDataReady(
                dbContext,
                configuration.GetConnectionString("DefaultConnection"),
                app.Environment.ContentRootPath);
        }

        ValidateOficioFolderNameCollisions(dbContext);
    }

    private static void PrepareApplicationData(
        AppDbContext dbContext,
        IConfiguration configuration,
        string? connectionString,
        string contentRootPath,
        IHostEnvironment environment,
        ILogger logger,
        bool resetApplicationData)
    {
        if (resetApplicationData)
        {
            dbContext.Database.EnsureDeleted();
        }

        InitializeDatabaseSchema(dbContext, configuration, environment, logger);
        EnsureSqliteCaseAuditSchema(dbContext);
        EnsureSqliteRequiredIndexes(dbContext);
        EnsureBootstrapAdminSeeded(dbContext, configuration, logger);
        ValidateApplicationDataReady(dbContext, connectionString, contentRootPath);
    }

    private static void InitializeDatabaseSchema(
        AppDbContext dbContext,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (HasLegacySchemaWithoutMigrationHistory(dbContext))
        {
            if (!IsLegacySchemaCompatibilityAllowed(configuration, environment))
            {
                throw new InvalidOperationException(
                    "Se detecto una base SQLite existente sin historial de migraciones de EF Core. " +
                    "Migra formalmente esa base antes de iniciar la aplicacion o habilita temporalmente " +
                    "Startup:AllowLegacySchemaWithoutMigrations=true.");
            }

            logger.LogWarning(
                "Se detecto una base SQLite existente sin historial de migraciones de EF Core. " +
                "Se mantiene compatibilidad legacy por configuracion temporal; planifica migrarla formalmente.");
            dbContext.Database.EnsureCreated();
            return;
        }

        dbContext.Database.Migrate();
    }

    private static bool IsLegacySchemaCompatibilityAllowed(IConfiguration configuration, IHostEnvironment environment)
    {
        return configuration.GetValue<bool>("Startup:AllowLegacySchemaWithoutMigrations");
    }

    private static void StampBaselineMigrationHistory(AppDbContext dbContext)
    {
        var baselineMigrationId = dbContext.Database.GetMigrations().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(baselineMigrationId))
        {
            throw new InvalidOperationException(
                "No se encontro ninguna migracion EF Core para registrar como baseline.");
        }

        var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "9.0.0";

        using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();

        using (var createTableCommand = connection.CreateCommand())
        {
            createTableCommand.Transaction = transaction;
            createTableCommand.CommandText =
                $"CREATE TABLE IF NOT EXISTS \"{EfMigrationHistoryTableName}\" (" +
                "\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                "\"ProductVersion\" TEXT NOT NULL);";
            createTableCommand.ExecuteNonQuery();
        }

        using var existsCommand = connection.CreateCommand();
        existsCommand.Transaction = transaction;
        existsCommand.CommandText =
            $"SELECT 1 FROM \"{EfMigrationHistoryTableName}\" WHERE \"MigrationId\" = $migrationId LIMIT 1;";
        var migrationIdParameter = existsCommand.CreateParameter();
        migrationIdParameter.ParameterName = "$migrationId";
        migrationIdParameter.Value = baselineMigrationId;
        existsCommand.Parameters.Add(migrationIdParameter);

        if (existsCommand.ExecuteScalar() is null)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                $"INSERT INTO \"{EfMigrationHistoryTableName}\" (\"MigrationId\", \"ProductVersion\") VALUES ($migrationId, $productVersion);";

            var insertMigrationIdParameter = insertCommand.CreateParameter();
            insertMigrationIdParameter.ParameterName = "$migrationId";
            insertMigrationIdParameter.Value = baselineMigrationId;
            insertCommand.Parameters.Add(insertMigrationIdParameter);

            var productVersionParameter = insertCommand.CreateParameter();
            productVersionParameter.ParameterName = "$productVersion";
            productVersionParameter.Value = productVersion;
            insertCommand.Parameters.Add(productVersionParameter);

            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static bool HasLegacySchemaWithoutMigrationHistory(AppDbContext dbContext)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return false;
        }

        using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        if (SqliteTableExists(connection, EfMigrationHistoryTableName))
        {
            return false;
        }

        return SqliteTableExists(connection, "Oficios") ||
               SqliteTableExists(connection, "CasosCorreccion") ||
               SqliteTableExists(connection, "EvidenciasFoto") ||
               SqliteTableExists(connection, "UsuariosSistema") ||
               SqliteTableExists(connection, "AuditLogs");
    }

    private static void ValidateStartupConfiguration(IHostEnvironment environment, bool resetApplicationData)
    {
        ValidateResetApplicationDataAllowed(environment, resetApplicationData);
    }

    private static void ValidateResetApplicationDataAllowed(IHostEnvironment environment, bool resetApplicationData)
    {
        if (!resetApplicationData)
        {
            return;
        }

        if (environment.IsDevelopment())
        {
            return;
        }

        throw new InvalidOperationException(
            "Startup:ResetApplicationData solo se permite en el entorno Development para evitar borrado accidental de datos.");
    }

    private static void ValidateApplicationDataReady(
        AppDbContext dbContext,
        string? connectionString,
        string contentRootPath)
    {
        ValidateSqliteDatabaseFileExists(connectionString, contentRootPath);

        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        EnsureSqliteCaseAuditSchema(dbContext);

        var requiredTables = new[]
        {
            "Oficios",
            "CasosCorreccion",
            "EvidenciasFoto",
            "UsuariosSistema",
            "AuditLogs"
        };

        var missingTables = new List<string>();
        using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        foreach (var tableName in requiredTables)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            if (command.ExecuteScalar() is null)
            {
                missingTables.Add(tableName);
            }
        }

        var validationErrors = new List<string>();
        if (missingTables.Count > 0)
        {
            validationErrors.Add($"Faltan tablas requeridas: {string.Join(", ", missingTables)}.");
        }

        if (!missingTables.Contains("CasosCorreccion", StringComparer.OrdinalIgnoreCase))
        {
            ValidateRequiredColumns(connection, "CasosCorreccion", ["Cct", "CctHash", "CurpHash", "FolioHash", "Validador", "AuditorUsuarioId", "NivelEducativo", "PeriodoInicio", "PeriodoFin", "FechaVerificacion", "EvidenciaUrl", "AuditoriaEstado", "AuditoriaUltimoResultado", "AuditoriaUltimaObservacion", "AuditoriaUltimoUsuarioId", "AuditoriaUltimoUsuario", "AuditoriaUltimaRevisionUtc"], validationErrors);
            ValidateRequiredTableSqlFragments(connection, "CasosCorreccion", ["CK_CasosCorreccion_Estatus", "CK_CasosCorreccion_NivelEducativo"], validationErrors);
            ValidateRequiredIndexColumns(connection, "CasosCorreccion", "IX_CasosCorreccion_OficioId_NivelEducativo", ["OficioId", "NivelEducativo"], validationErrors);
        }

        if (!missingTables.Contains("EvidenciasFoto", StringComparer.OrdinalIgnoreCase))
        {
            ValidateRequiredTableSqlFragments(connection, "EvidenciasFoto", ["CK_EvidenciasFoto_TipoEvidencia"], validationErrors);
        }

        if (!missingTables.Contains("UsuariosSistema", StringComparer.OrdinalIgnoreCase))
        {
            ValidateRequiredColumns(connection, "UsuariosSistema", ["MustChangePassword", "PasswordUpdatedAtUtc"], validationErrors);
        }

        if (validationErrors.Count == 0 && !dbContext.UsuariosSistema.AsNoTracking().Any())
        {
            validationErrors.Add("No existe ningun usuario en UsuariosSistema.");
        }

        if (validationErrors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "La base de datos no esta preparada para esta version. " +
            string.Join(' ', validationErrors) +
            " Ejecuta .\\scripts\\db\\Initialize-BitacoraDatabase.ps1 o inicia una vez con Startup__PrepareApplicationData=true.");
    }

    private static void ValidateSqliteDatabaseFileExists(string? connectionString, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se encontro ConnectionStrings:DefaultConnection. Configura la conexion antes de iniciar la aplicacion.");
        }

        var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = sqliteBuilder.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var absolutePath = Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.Combine(contentRootPath, dataSource);

        if (File.Exists(absolutePath))
        {
            return;
        }

        throw new InvalidOperationException(
            $"No se encontro la base de datos SQLite en '{absolutePath}'. " +
            "Preparala de forma explicita antes de iniciar la aplicacion.");
    }

    private static void ValidateRequiredColumns(
        System.Data.Common.DbConnection connection,
        string tableName,
        IReadOnlyCollection<string> requiredColumns,
        ICollection<string> validationErrors)
    {
        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName}');";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader["name"]?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                columnNames.Add(name);
            }
        }

        var missingColumns = requiredColumns
            .Where(columnName => !columnNames.Contains(columnName))
            .ToList();

        if (missingColumns.Count > 0)
        {
            validationErrors.Add($"La tabla {tableName} no tiene columnas requeridas: {string.Join(", ", missingColumns)}.");
        }
    }

    private static void ValidateRequiredTableSqlFragments(
        System.Data.Common.DbConnection connection,
        string tableName,
        IReadOnlyCollection<string> requiredFragments,
        ICollection<string> validationErrors)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var createTableSql = command.ExecuteScalar()?.ToString() ?? string.Empty;
        var missingFragments = requiredFragments
            .Where(fragment => !createTableSql.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (missingFragments.Count > 0)
        {
            validationErrors.Add($"La tabla {tableName} no tiene restricciones requeridas: {string.Join(", ", missingFragments)}.");
        }
    }

    private static void ValidateRequiredIndexColumns(
        System.Data.Common.DbConnection connection,
        string tableName,
        string indexName,
        IReadOnlyList<string> expectedColumns,
        ICollection<string> validationErrors)
    {
        using var indexListCommand = connection.CreateCommand();
        indexListCommand.CommandText = $"PRAGMA index_list('{tableName}');";

        var indexExists = false;
        using (var reader = indexListCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var currentIndexName = reader["name"]?.ToString();
                if (string.Equals(currentIndexName, indexName, StringComparison.OrdinalIgnoreCase))
                {
                    indexExists = true;
                    break;
                }
            }
        }

        if (!indexExists)
        {
            validationErrors.Add($"La tabla {tableName} no tiene el indice requerido {indexName}.");
            return;
        }

        using var indexInfoCommand = connection.CreateCommand();
        indexInfoCommand.CommandText = $"PRAGMA index_info('{indexName}');";

        var actualColumns = new List<string>();
        using (var reader = indexInfoCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                var columnName = reader["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    actualColumns.Add(columnName);
                }
            }
        }

        if (!actualColumns.SequenceEqual(expectedColumns, StringComparer.OrdinalIgnoreCase))
        {
            validationErrors.Add(
                $"El indice {indexName} de la tabla {tableName} no tiene las columnas esperadas: {string.Join(", ", expectedColumns)}.");
        }
    }

    private static void EnsureSqliteRequiredIndexes(AppDbContext dbContext)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_CasosCorreccion_OficioId_NivelEducativo
            ON CasosCorreccion (OficioId, NivelEducativo);
            CREATE INDEX IF NOT EXISTS IX_CasosCorreccion_CurpHash
            ON CasosCorreccion (CurpHash);
            CREATE INDEX IF NOT EXISTS IX_CasosCorreccion_CctHash
            ON CasosCorreccion (CctHash);
            CREATE INDEX IF NOT EXISTS IX_CasosCorreccion_FolioHash
            ON CasosCorreccion (FolioHash);
            CREATE INDEX IF NOT EXISTS IX_AuditLogs_CurpHash
            ON AuditLogs (CurpHash);
            CREATE INDEX IF NOT EXISTS IX_AuditLogs_CctHash
            ON AuditLogs (CctHash);
            CREATE INDEX IF NOT EXISTS IX_AuditLogs_FolioCertificadoHash
            ON AuditLogs (FolioCertificadoHash);
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureSqliteCaseAuditSchema(AppDbContext dbContext)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        if (!SqliteTableExists(connection, "CasosCorreccion"))
        {
            return;
        }

        EnsureSqliteColumn(connection, "CasosCorreccion", "AuditoriaEstado", "TEXT NOT NULL DEFAULT 'Pendiente'");
        EnsureSqliteColumn(connection, "CasosCorreccion", "AuditoriaUltimoResultado", "TEXT NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "AuditoriaUltimaObservacion", "TEXT NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "AuditoriaUltimoUsuarioId", "INTEGER NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "AuditoriaUltimoUsuario", "TEXT NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "Validador", "TEXT NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "CurpHash", "TEXT NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "CctHash", "TEXT NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "FolioHash", "TEXT NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "AuditorUsuarioId", "INTEGER NULL");
        EnsureSqliteColumn(connection, "CasosCorreccion", "AuditoriaUltimaRevisionUtc", "TEXT NULL");
        EnsureSqliteColumn(connection, "AuditLogs", "CurpHash", "TEXT NULL");
        EnsureSqliteColumn(connection, "AuditLogs", "CurpSuffix", "TEXT NULL");
        EnsureSqliteColumn(connection, "AuditLogs", "CctHash", "TEXT NULL");
        EnsureSqliteColumn(connection, "AuditLogs", "CctSuffix", "TEXT NULL");
        EnsureSqliteColumn(connection, "AuditLogs", "FolioCertificadoHash", "TEXT NULL");
        EnsureSqliteColumn(connection, "AuditLogs", "FolioCertificadoSuffix", "TEXT NULL");

        using var normalizeCommand = connection.CreateCommand();
        normalizeCommand.CommandText = """
            UPDATE CasosCorreccion
            SET AuditoriaEstado = 'Pendiente'
            WHERE AuditoriaEstado IS NULL OR trim(AuditoriaEstado) = '';
            """;
        normalizeCommand.ExecuteNonQuery();
    }

    private static bool SqliteTableExists(System.Data.Common.DbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        return command.ExecuteScalar() is not null;
    }

    private static void EnsureSqliteColumn(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        using var tableInfoCommand = connection.CreateCommand();
        tableInfoCommand.CommandText = $"PRAGMA table_info('{tableName}');";

        using var reader = tableInfoCommand.ExecuteReader();
        while (reader.Read())
        {
            var currentName = reader["name"]?.ToString();
            if (string.Equals(currentName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }

    private static void ValidateOficioFolderNameCollisions(AppDbContext dbContext)
    {
        var numerosOficio = dbContext.Oficios
            .AsNoTracking()
            .Select(x => x.NumeroOficio)
            .OrderBy(x => x)
            .ToList();

        if (!OficioStoragePath.TryFindAnyFolderCollision(numerosOficio, out var numeroOficio, out var conflictingNumero))
        {
            return;
        }

        var folderName = OficioStoragePath.ToFolderName(numeroOficio!);
        throw new InvalidOperationException(
            $"Se detecto una colision de carpetas de oficio para '{numeroOficio}' y '{conflictingNumero}'. " +
            $"Ambos valores producen la carpeta '{folderName}'. Corrige los numeros antes de iniciar la aplicacion.");
    }

    private static void EnsureBootstrapAdminSeeded(
        AppDbContext dbContext,
        IConfiguration configuration,
        ILogger logger)
    {
        if (dbContext.UsuariosSistema.Any())
        {
            return;
        }

        var configuredBootstrapPassword = configuration["Security:BootstrapAdminPassword"]?.Trim();
        var bootstrapPassword = string.IsNullOrWhiteSpace(configuredBootstrapPassword)
            ? TemporaryPasswordGenerator.Generate(16)
            : configuredBootstrapPassword;

        var (passwordHash, passwordSalt) = PasswordHasher.HashPassword(bootstrapPassword);
        dbContext.UsuariosSistema.Add(new UsuarioSistema
        {
            Usuario = "admin",
            UsuarioNormalizado = "ADMIN",
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Rol = SecurityDefaults.AdminRole,
            Activo = true,
            MustChangePassword = true,
            CreadoEnUtc = DateTime.UtcNow
        });

        dbContext.SaveChanges();

        if (string.IsNullOrWhiteSpace(configuredBootstrapPassword))
        {
            logger.LogWarning(
                "Bootstrap admin creado. Usuario: admin. Se genero una contrasena temporal que no sera registrada en logs. Debe cambiarse en el primer acceso.");
        }
        else
        {
            logger.LogWarning(
                "Bootstrap admin creado. Usuario: admin. Se uso una contrasena temporal configurada externamente y no fue registrada en logs. Debe cambiarse en el primer acceso.");
        }
    }
}


