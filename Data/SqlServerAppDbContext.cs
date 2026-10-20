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
        // The report notification triggers run in the source transaction. OUTPUT
        // without INTO is incompatible with enabled triggers on these tables.
        modelBuilder.Entity<Models.Oficio>().ToTable(t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<Models.CasoCorreccion>().ToTable(t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<Models.EvidenciaFoto>().ToTable(t => t.UseSqlOutputClause(false));
        modelBuilder.Entity<Models.Oficio>()
            .HasIndex(o => new { o.FechaOficio, o.NumeroOficio, o.Id })
            .IsDescending(true, false, false).IncludeProperties(nameof(Models.Oficio.Asunto));
        modelBuilder.Entity<Models.CasoCorreccion>()
            .HasIndex(c => new { c.OficioId, c.AuditoriaEstado })
            .IncludeProperties(nameof(Models.CasoCorreccion.EvidenciaUrl), nameof(Models.CasoCorreccion.Consecutivo));
        modelBuilder.Entity<Models.CasoCorreccion>()
            .HasIndex(c => new { c.Estatus, c.CreadoEn, c.Id }).IsDescending(false, true, true);
        modelBuilder.Entity<Models.CasoCorreccion>()
            .HasIndex(c => new { c.CreadoEn, c.Id }).IsDescending(true, true);
        modelBuilder.Entity<Models.CasoCorreccion>()
            .HasIndex(c => new { c.FechaVerificacion, c.Id }).IsDescending(true, true);
        modelBuilder.Entity<Models.AuditLog>()
            .HasIndex(a => new { a.OccurredAtUtc, a.Id }).IsDescending(true, true);
        modelBuilder.Entity<Models.UsuarioSistema>()
            .HasIndex(u => new { u.Usuario, u.Id });
        modelBuilder.Entity<Models.EvidenciaFoto>()
            .HasIndex(e => e.CasoCorreccionId);
        modelBuilder.Entity<Models.EvidenciaFoto>()
            .HasIndex(e => new { e.CasoCorreccionId, e.FechaEvidencia, e.Id }).IsDescending(false, true, true);
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
