using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Data;

public static class DbUpdateExceptionExtensions
{
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueConstraintExtendedCode = 2067;

    public static bool IsUniqueConstraintViolation(this DbUpdateException exception, string? constraintFragment = null)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        if (sqliteException.SqliteErrorCode != SqliteConstraintErrorCode)
        {
            return false;
        }

        var isUniqueConstraint = sqliteException.SqliteExtendedErrorCode == SqliteUniqueConstraintExtendedCode ||
                                 sqliteException.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);

        if (!isUniqueConstraint)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(constraintFragment) ||
               sqliteException.Message.Contains(constraintFragment, StringComparison.OrdinalIgnoreCase);
    }
}
