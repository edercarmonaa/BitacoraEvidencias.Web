using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BitacoraEvidencias.Web.Data;

public class AppDbContext : DbContext
{
    private readonly ISensitiveDataProtectionService? _sensitiveDataProtection;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ISensitiveDataProtectionService sensitiveDataProtection)
        : base(options)
    {
        _sensitiveDataProtection = sensitiveDataProtection;
    }

    public DbSet<Oficio> Oficios => Set<Oficio>();
    public DbSet<CasoCorreccion> CasosCorreccion => Set<CasoCorreccion>();
    public DbSet<EvidenciaFoto> EvidenciasFoto => Set<EvidenciaFoto>();
    public DbSet<UsuarioSistema> UsuariosSistema => Set<UsuarioSistema>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var sensitiveStringConverter = CreateSensitiveStringConverter();

        var estatusConstraint = BuildInConstraint("\"Estatus\"", CatalogosCaso.Estatus);
        var tipoEvidenciaConstraint = BuildInConstraint("\"TipoEvidencia\"", CatalogosCaso.TiposEvidencia);
        var nivelEducativoConstraint = BuildInConstraint("\"NivelEducativo\"", CatalogosCaso.NivelesEducativos);
        var auditoriaEstadoConstraint = BuildInConstraint("\"AuditoriaEstado\"", CatalogosAuditoriaCaso.Estados);

        modelBuilder.Entity<Oficio>(entity =>
        {
            entity.HasIndex(x => x.NumeroOficio).IsUnique();
            entity.Property(x => x.NumeroOficio).HasMaxLength(80);
            entity.Property(x => x.Asunto).HasMaxLength(200);
            entity.Property(x => x.Notas).HasMaxLength(255);
            entity.HasMany(x => x.Casos)
                .WithOne(x => x.Oficio)
                .HasForeignKey(x => x.OficioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CasoCorreccion>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("CK_CasosCorreccion_Estatus", estatusConstraint));
            entity.ToTable(table => table.HasCheckConstraint("CK_CasosCorreccion_NivelEducativo", nivelEducativoConstraint));
            entity.ToTable(table => table.HasCheckConstraint("CK_CasosCorreccion_AuditoriaEstado", auditoriaEstadoConstraint));
            entity.HasIndex(x => new { x.OficioId, x.Consecutivo }).IsUnique();
            entity.HasIndex(x => new { x.OficioId, x.NivelEducativo }).IsUnique();
            entity.HasIndex(x => x.Curp);
            entity.HasIndex(x => x.CurpHash);
            entity.HasIndex(x => x.Folio);
            entity.HasIndex(x => x.FolioHash);
            entity.HasIndex(x => x.Cct);
            entity.HasIndex(x => x.CctHash);
            entity.HasIndex(x => x.Estatus);
            entity.HasIndex(x => x.NivelEducativo);
            entity.HasIndex(x => x.AuditoriaEstado);
            entity.Property(x => x.Curp).HasMaxLength(512).HasConversion(sensitiveStringConverter);
            entity.Property(x => x.CurpHash).HasMaxLength(64);
            entity.Property(x => x.NombreCompleto).HasMaxLength(512).HasConversion(sensitiveStringConverter);
            entity.Property(x => x.Cct).HasMaxLength(512).HasConversion(sensitiveStringConverter);
            entity.Property(x => x.CctHash).HasMaxLength(64);
            entity.Property(x => x.Validador).HasMaxLength(512).HasConversion(sensitiveStringConverter);
            entity.Property(x => x.NivelEducativo).HasMaxLength(20);
            entity.Property(x => x.TipoCorreccion).HasMaxLength(80);
            entity.Property(x => x.Folio).HasMaxLength(512).HasConversion(sensitiveStringConverter);
            entity.Property(x => x.FolioHash).HasMaxLength(64);
            entity.Property(x => x.EvidenciaUrl).HasMaxLength(1600).HasConversion(sensitiveStringConverter);
            entity.Property(x => x.Observaciones).HasConversion(sensitiveStringConverter);
            entity.Property(x => x.Estatus).HasMaxLength(40);
            entity.Property(x => x.AuditoriaEstado).HasMaxLength(20);
            entity.Property(x => x.AuditoriaUltimoResultado).HasMaxLength(20);
            entity.Property(x => x.AuditoriaUltimaObservacion).HasMaxLength(100);
            entity.Property(x => x.AuditoriaUltimoUsuario).HasMaxLength(60);
            entity.HasOne(x => x.AuditorUsuario)
                .WithMany()
                .HasForeignKey(x => x.AuditorUsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EvidenciaFoto>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("CK_EvidenciasFoto_TipoEvidencia", tipoEvidenciaConstraint));
            entity.Property(x => x.TipoEvidencia).HasMaxLength(80);
            entity.Property(x => x.RutaArchivo).HasMaxLength(500);
            entity.Property(x => x.RutaMiniatura).HasMaxLength(500);
            entity.HasOne(x => x.CasoCorreccion)
                .WithMany(x => x.Evidencias)
                .HasForeignKey(x => x.CasoCorreccionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UsuarioSistema>(entity =>
        {
            entity.HasIndex(x => x.UsuarioNormalizado).IsUnique();
            entity.HasIndex(x => new { x.Rol, x.Activo });
            entity.Property(x => x.Usuario).HasMaxLength(60);
            entity.Property(x => x.UsuarioNormalizado).HasMaxLength(60);
            entity.Property(x => x.PasswordHash).HasMaxLength(200);
            entity.Property(x => x.PasswordSalt).HasMaxLength(200);
            entity.Property(x => x.Rol).HasMaxLength(20);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.EventType);
            entity.HasIndex(x => x.Username);
            entity.HasIndex(x => x.NumeroOficio);
            entity.HasIndex(x => x.CurpHash);
            entity.HasIndex(x => x.CctHash);
            entity.HasIndex(x => x.FolioCertificadoHash);
            entity.Property(x => x.EventType).HasMaxLength(60);
            entity.Property(x => x.EntityType).HasMaxLength(40);
            entity.Property(x => x.Username).HasMaxLength(60);
            entity.Property(x => x.Role).HasMaxLength(20);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.Property(x => x.NumeroOficio).HasMaxLength(80);
            entity.Property(x => x.Curp).HasMaxLength(18);
            entity.Property(x => x.CurpHash).HasMaxLength(64);
            entity.Property(x => x.CurpSuffix).HasMaxLength(8);
            entity.Property(x => x.Cct).HasMaxLength(30);
            entity.Property(x => x.CctHash).HasMaxLength(64);
            entity.Property(x => x.CctSuffix).HasMaxLength(8);
            entity.Property(x => x.FolioCertificado).HasMaxLength(80);
            entity.Property(x => x.FolioCertificadoHash).HasMaxLength(64);
            entity.Property(x => x.FolioCertificadoSuffix).HasMaxLength(8);
            entity.Property(x => x.Details).HasMaxLength(2000);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareSensitiveData();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareSensitiveData();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private ValueConverter<string?, string?>? CreateSensitiveStringConverter()
    {
        if (_sensitiveDataProtection?.IsConfigured != true)
        {
            return null;
        }

        return new ValueConverter<string?, string?>(
            value => _sensitiveDataProtection.ProtectString(value),
            value => _sensitiveDataProtection.UnprotectString(value));
    }

    private void PrepareSensitiveData()
    {
        if (_sensitiveDataProtection?.IsConfigured != true)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<CasoCorreccion>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.CurpHash = _sensitiveDataProtection.BlindIndex(entry.Entity.Curp);
                entry.Entity.CctHash = _sensitiveDataProtection.BlindIndex(entry.Entity.Cct);
                entry.Entity.FolioHash = _sensitiveDataProtection.BlindIndex(entry.Entity.Folio);
            }
        }

        foreach (var entry in ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            PrepareAuditSensitiveField(
                entry,
                nameof(AuditLog.Curp),
                nameof(AuditLog.CurpHash),
                nameof(AuditLog.CurpSuffix));
            PrepareAuditSensitiveField(
                entry,
                nameof(AuditLog.Cct),
                nameof(AuditLog.CctHash),
                nameof(AuditLog.CctSuffix));
            PrepareAuditSensitiveField(
                entry,
                nameof(AuditLog.FolioCertificado),
                nameof(AuditLog.FolioCertificadoHash),
                nameof(AuditLog.FolioCertificadoSuffix));
        }
    }

    private void PrepareAuditSensitiveField(
        EntityEntry<AuditLog> entry,
        string valuePropertyName,
        string hashPropertyName,
        string suffixPropertyName)
    {
        var valueProperty = entry.Property<string?>(valuePropertyName);
        var value = valueProperty.CurrentValue;
        entry.Property<string?>(hashPropertyName).CurrentValue = _sensitiveDataProtection!.BlindIndex(value);
        entry.Property<string?>(suffixPropertyName).CurrentValue = _sensitiveDataProtection.Suffix(value);
        valueProperty.CurrentValue = null;
    }

    private static string BuildInConstraint(string columnName, IEnumerable<string> values)
    {
        var escapedValues = values.Select(value => $"'{value.Replace("'", "''")}'");
        return $"{columnName} IN ({string.Join(", ", escapedValues)})";
    }
}
