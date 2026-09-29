using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface ISqlAdminService
{
    Task<SqlExecutionResult> ExecuteAsync(string sql, int maxRows, CancellationToken cancellationToken = default);
}

public sealed class SqlExecutionResult : ApplicationCommandResult
{
    public string StatementType { get; init; } = string.Empty;
    public int MaxRows { get; init; }
    public int ReturnedRows { get; init; }
    public int? AffectedRows { get; init; }
    public IReadOnlyList<string> Columns { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; init; } = [];
}

public sealed class SqlAdminService(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : ISqlAdminService
{
    private static readonly IReadOnlySet<string> AllowedStatements = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT",
        "INSERT",
        "UPDATE",
        "DELETE"
    };

    private static readonly IReadOnlySet<string> BlockedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DROP",
        "ALTER",
        "TRUNCATE",
        "ATTACH",
        "PRAGMA"
    };

    public async Task<SqlExecutionResult> ExecuteAsync(string sql, int maxRows, CancellationToken cancellationToken = default)
    {
        var normalizedSql = sql?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedSql))
        {
            return new SqlExecutionResult
            {
                ValidationErrors = [new("Sql", "La sentencia SQL es obligatoria.")]
            };
        }

        if (ContainsMultipleStatements(normalizedSql))
        {
            return new SqlExecutionResult
            {
                StatementType = GetStatementType(normalizedSql),
                MaxRows = maxRows,
                ValidationErrors = [new("Sql", "Solo se permite una sentencia SQL por ejecucion.")]
            };
        }

        var blockedToken = FindBlockedToken(normalizedSql);
        if (blockedToken is not null)
        {
            return new SqlExecutionResult
            {
                StatementType = GetStatementType(normalizedSql),
                MaxRows = maxRows,
                ValidationErrors = [new("Sql", $"La sentencia contiene una operacion bloqueada: {blockedToken}.")]
            };
        }

        var statementType = GetStatementType(normalizedSql);
        if (string.IsNullOrWhiteSpace(statementType) || !AllowedStatements.Contains(statementType))
        {
            return new SqlExecutionResult
            {
                StatementType = statementType,
                MaxRows = maxRows,
                ValidationErrors = [new("Sql", "Solo se permiten sentencias SELECT, INSERT, UPDATE o DELETE.")]
            };
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = normalizedSql;
            command.CommandType = CommandType.Text;

            SqlExecutionResult result;
            if (string.Equals(statementType, "SELECT", StringComparison.OrdinalIgnoreCase))
            {
                result = await ExecuteSelectAsync(command, maxRows, cancellationToken);
            }
            else
            {
                var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
                result = new SqlExecutionResult
                {
                    StatementType = statementType,
                    AffectedRows = affectedRows,
                    MaxRows = maxRows
                };
            }

            await auditLogService.WriteAsync(new AuditLogEntry
            {
                EventType = AuditEvents.Update,
                EntityType = "AdminSql",
                Details = BuildAuditSummary(statementType, result),
                Success = true
            }, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            await auditLogService.WriteAsync(new AuditLogEntry
            {
                EventType = AuditEvents.Update,
                EntityType = "AdminSql",
                Details = $"SQL admin fallo. Tipo: {statementType}.",
                Success = false
            }, cancellationToken);

            return new SqlExecutionResult
            {
                StatementType = statementType,
                ErrorMessage = ex.Message,
                MaxRows = maxRows
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<SqlExecutionResult> ExecuteSelectAsync(DbCommand command, int maxRows, CancellationToken cancellationToken)
    {
        var columns = new List<string>();
        var rows = new List<IReadOnlyList<string?>>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            columns.Add(reader.GetName(index));
        }

        while (rows.Count < maxRows && await reader.ReadAsync(cancellationToken))
        {
            var row = new string?[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[index] = reader.IsDBNull(index) ? null : Convert.ToString(reader.GetValue(index));
            }

            rows.Add(row);
        }

        return new SqlExecutionResult
        {
            StatementType = "SELECT",
            MaxRows = maxRows,
            ReturnedRows = rows.Count,
            Columns = columns,
            Rows = rows
        };
    }

    private static string BuildAuditSummary(string statementType, SqlExecutionResult result)
    {
        if (string.Equals(statementType, "SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return $"SQL admin SELECT ejecutado. Columnas: {result.Columns.Count}. Filas devueltas: {result.ReturnedRows}. Limite: {result.MaxRows}.";
        }

        return $"SQL admin {statementType} ejecutado. Filas afectadas: {result.AffectedRows ?? 0}.";
    }

    private static string GetStatementType(string sql)
    {
        var match = Regex.Match(sql, "^\\s*([A-Za-z]+)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
    }

    private static string? FindBlockedToken(string sql)
    {
        foreach (var token in BlockedTokens)
        {
            if (Regex.IsMatch(sql, $"\\b{Regex.Escape(token)}\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return token;
            }
        }

        return null;
    }

    private static bool ContainsMultipleStatements(string sql)
    {
        var trimmed = sql.Trim();
        if (trimmed.EndsWith(';'))
        {
            trimmed = trimmed[..^1].TrimEnd();
        }

        return trimmed.Contains(';');
    }
}

