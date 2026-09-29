using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

if (args.Length < 1)
{
    Console.Error.WriteLine("Uso: BitacoraMonitorQuery <db-path> [hours-back]");
    Console.Error.WriteLine("   o: BitacoraMonitorQuery archive <db-path> <archive-root> [retention-days]");
    return 1;
}

if (string.Equals(args[0], "archive", StringComparison.OrdinalIgnoreCase))
{
    return await RunArchiveAsync(args.Skip(1).ToArray());
}

return await RunQueryAsync(args);

static async Task<int> RunQueryAsync(string[] args)
{
    var dbPath = args[0];
    var hoursBack = 24;
    if (args.Length >= 2 && (!int.TryParse(args[1], out hoursBack) || hoursBack <= 0))
    {
        Console.Error.WriteLine("hours-back debe ser un entero positivo.");
        return 1;
    }

    if (!File.Exists(dbPath))
    {
        Console.Error.WriteLine($"No existe la base: {dbPath}");
        return 1;
    }

    var cutoffUtc = DateTime.UtcNow.AddHours(-hoursBack);
    var trackedEvents = new[]
    {
        "LOGIN_FAIL",
        "LOCKOUT",
        "DELETE",
        "DELETE_EVIDENCE"
    };

    var results = trackedEvents.ToDictionary(
        key => key,
        _ => 0L,
        StringComparer.OrdinalIgnoreCase);

    var connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = dbPath,
        Mode = SqliteOpenMode.ReadOnly,
        DefaultTimeout = 5
    }.ToString();

    await using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();

    await using (var pragmaCommand = connection.CreateCommand())
    {
        pragmaCommand.CommandText = "PRAGMA busy_timeout = 5000;";
        await pragmaCommand.ExecuteNonQueryAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText =
        """
        SELECT EventType, COUNT(*) AS Total
        FROM AuditLogs
        WHERE OccurredAtUtc >= $cutoffUtc
          AND EventType IN ('LOGIN_FAIL', 'LOCKOUT', 'DELETE', 'DELETE_EVIDENCE')
        GROUP BY EventType
        ORDER BY EventType;
        """;
    command.Parameters.AddWithValue("$cutoffUtc", cutoffUtc.ToString("O"));

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var eventType = reader.GetString(0);
        var total = reader.GetInt64(1);
        results[eventType] = total;
    }

    foreach (var pair in results.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"{pair.Key}={pair.Value}");
    }

    return 0;
}

static async Task<int> RunArchiveAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Uso: BitacoraMonitorQuery archive <db-path> <archive-root> [retention-days]");
        return 1;
    }

    var dbPath = args[0];
    var archiveRoot = args[1];
    var retentionDays = 30;
    if (args.Length >= 3 && (!int.TryParse(args[2], out retentionDays) || retentionDays <= 0))
    {
        Console.Error.WriteLine("retention-days debe ser un entero positivo.");
        return 1;
    }

    if (!File.Exists(dbPath))
    {
        Console.Error.WriteLine($"No existe la base: {dbPath}");
        return 1;
    }

    Directory.CreateDirectory(archiveRoot);

    var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
    var totalExported = 0;
    var totalDeleted = 0;
    var exportedFiles = new List<string>();

    var connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = dbPath,
        Mode = SqliteOpenMode.ReadWrite,
        DefaultTimeout = 30
    }.ToString();

    await using var connection = new SqliteConnection(connectionString);
    await connection.OpenAsync();

    await using (var pragmaCommand = connection.CreateCommand())
    {
        pragmaCommand.CommandText = "PRAGMA busy_timeout = 5000;";
        await pragmaCommand.ExecuteNonQueryAsync();
    }

    var archiveDates = new List<DateOnly>();
    await using (var dateCommand = connection.CreateCommand())
    {
        dateCommand.CommandText =
            """
            SELECT DISTINCT date(OccurredAtUtc)
            FROM AuditLogs
            WHERE OccurredAtUtc < $cutoffUtc
            ORDER BY date(OccurredAtUtc);
            """;
        dateCommand.Parameters.AddWithValue("$cutoffUtc", cutoffUtc.ToString("O"));

        await using var reader = await dateCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            var value = reader.GetString(0);
            if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                archiveDates.Add(date);
            }
        }
    }

    foreach (var archiveDate in archiveDates)
    {
        var dayStartUtc = archiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);
        var rows = new List<AuditLogRow>();

        await using (var exportCommand = connection.CreateCommand())
        {
            exportCommand.CommandText =
                """
                SELECT Id, OccurredAtUtc, EventType, EntityType, EntityId, UserId, Username, Role,
                       IpAddress, UserAgent, NumeroOficio, Consecutivo, Curp, Cct, FolioCertificado,
                       EvidenciasCount, Details, Success
                FROM AuditLogs
                WHERE OccurredAtUtc >= $dayStartUtc
                  AND OccurredAtUtc < $dayEndUtc
                  AND OccurredAtUtc < $cutoffUtc
                ORDER BY Id;
                """;
            exportCommand.Parameters.AddWithValue("$dayStartUtc", dayStartUtc.ToString("O"));
            exportCommand.Parameters.AddWithValue("$dayEndUtc", dayEndUtc.ToString("O"));
            exportCommand.Parameters.AddWithValue("$cutoffUtc", cutoffUtc.ToString("O"));

            await using var reader = await exportCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new AuditLogRow(
                    reader.GetInt64(0),
                    reader.GetDateTime(1),
                    reader.GetString(2),
                    ReadNullableString(reader, 3),
                    ReadNullableInt(reader, 4),
                    ReadNullableInt(reader, 5),
                    ReadNullableString(reader, 6),
                    ReadNullableString(reader, 7),
                    ReadNullableString(reader, 8),
                    ReadNullableString(reader, 9),
                    ReadNullableString(reader, 10),
                    ReadNullableInt(reader, 11),
                    ReadNullableString(reader, 12),
                    ReadNullableString(reader, 13),
                    ReadNullableString(reader, 14),
                    ReadNullableInt(reader, 15),
                    ReadNullableString(reader, 16),
                    reader.GetBoolean(17)));
            }
        }

        if (rows.Count == 0)
        {
            continue;
        }

        var archiveFileName = $"AuditLog_{archiveDate:yyyyMMdd}.log";
        var archiveFilePath = Path.Combine(archiveRoot, archiveFileName);
        var tempFilePath = archiveFilePath + ".tmp";

        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(FormatLogLine(row));
        }

        await File.WriteAllTextAsync(tempFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(tempFilePath, archiveFilePath, overwrite: true);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        await using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText =
            """
            DELETE FROM AuditLogs
            WHERE OccurredAtUtc >= $dayStartUtc
              AND OccurredAtUtc < $dayEndUtc
              AND OccurredAtUtc < $cutoffUtc;
            """;
        deleteCommand.Parameters.AddWithValue("$dayStartUtc", dayStartUtc.ToString("O"));
        deleteCommand.Parameters.AddWithValue("$dayEndUtc", dayEndUtc.ToString("O"));
        deleteCommand.Parameters.AddWithValue("$cutoffUtc", cutoffUtc.ToString("O"));

        var deleted = await deleteCommand.ExecuteNonQueryAsync();
        await transaction.CommitAsync();

        totalExported += rows.Count;
        totalDeleted += deleted;
        exportedFiles.Add(archiveFilePath);
    }

    Console.WriteLine($"RetentionDays={retentionDays}");
    Console.WriteLine($"CutoffUtc={cutoffUtc:O}");
    Console.WriteLine($"ExportedRows={totalExported}");
    Console.WriteLine($"DeletedRows={totalDeleted}");
    Console.WriteLine($"ExportedFiles={exportedFiles.Count}");

    foreach (var file in exportedFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"File={file}");
    }

    return 0;
}

static string FormatLogLine(AuditLogRow row)
{
    static string Serialize(string name, string? value)
    {
        return $"{name}={JsonSerializer.Serialize(value ?? string.Empty)}";
    }

    var parts = new List<string>
    {
        $"OccurredAtUtc={row.OccurredAtUtc:O}",
        $"Id={row.Id}",
        Serialize("EventType", row.EventType),
        $"Success={row.Success.ToString().ToLowerInvariant()}",
        Serialize("EntityType", row.EntityType),
        FormatNullable("EntityId", row.EntityId),
        FormatNullable("UserId", row.UserId),
        Serialize("Username", row.Username),
        Serialize("Role", row.Role),
        Serialize("IpAddress", row.IpAddress),
        Serialize("UserAgent", row.UserAgent),
        Serialize("NumeroOficio", row.NumeroOficio),
        FormatNullable("Consecutivo", row.Consecutivo),
        Serialize("Curp", row.Curp),
        Serialize("Cct", row.Cct),
        Serialize("FolioCertificado", row.FolioCertificado),
        FormatNullable("EvidenciasCount", row.EvidenciasCount),
        Serialize("Details", row.Details)
    };

    return string.Join(" | ", parts);
}

static string FormatNullable(string name, int? value)
{
    return value.HasValue ? $"{name}={value.Value}" : $"{name}=null";
}

static int? ReadNullableInt(SqliteDataReader reader, int index)
{
    return reader.IsDBNull(index) ? null : reader.GetInt32(index);
}

static string? ReadNullableString(SqliteDataReader reader, int index)
{
    return reader.IsDBNull(index) ? null : reader.GetString(index);
}

internal sealed record AuditLogRow(
    long Id,
    DateTime OccurredAtUtc,
    string EventType,
    string? EntityType,
    int? EntityId,
    int? UserId,
    string? Username,
    string? Role,
    string? IpAddress,
    string? UserAgent,
    string? NumeroOficio,
    int? Consecutivo,
    string? Curp,
    string? Cct,
    string? FolioCertificado,
    int? EvidenciasCount,
    string? Details,
    bool Success);
