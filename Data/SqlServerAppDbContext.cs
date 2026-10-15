using BitacoraEvidencias.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BitacoraEvidencias.Web.Data;

// A distinct context keeps SQL Server migrations separate from the SQLite baseline.
public sealed class SqlServerAppDbContext : AppDbContext
{
    public SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options)
        : base(options, null) { }

    public SqlServerAppDbContext(
        DbContextOptions<SqlServerAppDbContext> options,
        ISensitiveDataProtectionService protection)
        : base(options, protection) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // SQLite comparisons are case-sensitive. Do not merge distinct legacy values.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
                property.SetCollation("Latin1_General_100_BIN2");
    }
}

public sealed class SqlServerDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args)
    {
        // Scaffolding does not connect. Real connections must come from external configuration.
        var options = new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer("Server=localhost;Database=BitacoraDesignOnly;Integrated Security=true;Encrypt=true")
            .Options;
        return new SqlServerAppDbContext(options);
    }
}
