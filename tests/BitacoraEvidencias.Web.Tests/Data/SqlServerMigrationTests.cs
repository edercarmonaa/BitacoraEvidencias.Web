using System.Data;
using BitacoraEvidencias.Migration;
using BitacoraEvidencias.Web.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Data;

public class SqlServerMigrationTests
{
    [Fact]
    public void PagingUpgrade_OnlyCreatesIndexes()
    {
        var migration = new BitacoraEvidencias.Web.Data.SqlServerMigrations.PagingIndexes();
        Assert.Equal(8, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, operation => Assert.IsType<Microsoft.EntityFrameworkCore.Migrations.Operations.CreateIndexOperation>(operation));
    }

    [Fact]
    public void IncorrectEvidenceReport_TranslatesPagingAndFiltersToSqlServer()
    {
        using var context = new SqlServerAppDbContext(new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer("Server=localhost;Database=DesignOnly;Integrated Security=true;Encrypt=true").Options);
        var sql = BitacoraEvidencias.Web.Services.IncorrectAuditOfficeQueries.Cases(context).Skip(20).Take(20).ToQueryString();
        Assert.Contains("OFFSET", sql);
        Assert.Contains("FETCH NEXT", sql);
        Assert.Contains("TOP", BitacoraEvidencias.Web.Services.IncorrectAuditOfficeQueries.Offices(context).Take(5).ToQueryString());
    }

    [Fact]
    public void SqlServerSchema_HasIndependentMigrationAndMatchesSnapshot()
    {
        using var context = new SqlServerAppDbContext(new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer("Server=localhost;Database=DesignOnly;Integrated Security=true;Encrypt=true").Options);
        var migrations = context.Database.GetMigrations().ToArray();
        Assert.Equal(2, migrations.Length);
        Assert.EndsWith("_PagingIndexes", migrations[1]);
        Assert.EndsWith("_InitialSqlServer", migrations[0]);
        Assert.False(context.Database.HasPendingModelChanges());
        var script = context.Database.GenerateCreateScript();
        Assert.Contains("IDENTITY", script);
        Assert.Contains("datetime2", script);
        Assert.Contains("COLLATE Latin1_General_100_BIN2", script);
        Assert.DoesNotContain("ALTER DATABASE", script);
        Assert.DoesNotContain("PRAGMA", script);
        Assert.DoesNotContain("AUTOINCREMENT", script);
    }

    [Fact]
    public void SQLiteMigrationsRemainSeparate()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        Assert.DoesNotContain(context.Database.GetMigrations(), m => m.EndsWith("_InitialSqlServer"));
    }

    [Fact]
    public void LegacyConversion_PreservesPrecisionAndNullableValues()
    {
        var date = (DateTime)MigrationRunner.ConvertValue("2026-10-15 12:34:56.1234567", typeof(DateTime));
        Assert.Equal(1234567, date.Ticks % TimeSpan.TicksPerSecond);
        Assert.Same(DBNull.Value, MigrationRunner.ConvertValue(DBNull.Value, typeof(string)));
        Assert.False((bool)MigrationRunner.ConvertValue(0L, typeof(bool)));
        Assert.True((bool)MigrationRunner.ConvertValue(1L, typeof(bool)));
        Assert.Throws<MigrationValidationException>(() => MigrationRunner.ConvertValue(2L, typeof(bool)));
        Assert.Equal(long.MaxValue, MigrationRunner.ConvertValue(long.MaxValue, typeof(long)));
        Assert.Throws<OverflowException>(() => MigrationRunner.ConvertValue(long.MaxValue, typeof(int)));
    }

    [Fact]
    public void FullRowFingerprint_DetectsNullEmptyAndColumnBoundaryDifferences()
    {
        static DataTable Row(object first, object second)
        {
            var table = new DataTable();
            table.Columns.Add("First", typeof(string));
            table.Columns.Add("Second", typeof(string));
            table.Rows.Add(first, second);
            return table;
        }
        Assert.NotEqual(MigrationRunner.Digest(Row(DBNull.Value, "a")), MigrationRunner.Digest(Row("", "a")));
        Assert.NotEqual(MigrationRunner.Digest(Row("ab", "c")), MigrationRunner.Digest(Row("a", "bc")));
        Assert.Equal(MigrationRunner.Digest(Row("enc:v1:unchanged", "salt")), MigrationRunner.Digest(Row("enc:v1:unchanged", "salt")));
    }
}
