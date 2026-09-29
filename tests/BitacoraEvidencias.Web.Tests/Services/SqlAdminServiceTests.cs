using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Services;

public class SqlAdminServiceTests
{
    [Fact]
    public async Task ExecuteAsync_BlocksDangerousStatements()
    {
        await using var fixture = await SqlAdminFixture.CreateAsync();
        var service = new SqlAdminService(fixture.DbContext, fixture.AuditLogService);

        var result = await service.ExecuteAsync("DROP TABLE UsuariosSistema", 100);

        var validationError = Assert.Single(result.ValidationErrors);
        Assert.Equal("Sql", validationError.Key);
        Assert.Contains("DROP", validationError.Message);
        Assert.DoesNotContain(fixture.AuditLogService.Entries, entry => entry.EntityType == "AdminSql");
    }

    [Fact]
    public async Task ExecuteAsync_BlocksMultipleStatements()
    {
        await using var fixture = await SqlAdminFixture.CreateAsync();
        var service = new SqlAdminService(fixture.DbContext, fixture.AuditLogService);

        var result = await service.ExecuteAsync("SELECT 1; SELECT 2", 100);

        var validationError = Assert.Single(result.ValidationErrors);
        Assert.Equal("Sql", validationError.Key);
        Assert.Contains("una sentencia", validationError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.AuditLogService.Entries, entry => entry.EntityType == "AdminSql");
    }

    [Fact]
    public async Task ExecuteAsync_SelectReturnsColumnsAndRespectsLimit()
    {
        await using var fixture = await SqlAdminFixture.CreateAsync();
        fixture.DbContext.UsuariosSistema.AddRange(
            CreateUser("admin01"),
            CreateUser("admin02"),
            CreateUser("admin03"));
        await fixture.DbContext.SaveChangesAsync();

        var service = new SqlAdminService(fixture.DbContext, fixture.AuditLogService);

        var result = await service.ExecuteAsync(
            "SELECT Id, Usuario FROM UsuariosSistema ORDER BY Id",
            2);

        Assert.True(result.Succeeded);
        Assert.Equal("SELECT", result.StatementType);
        Assert.Equal(2, result.ReturnedRows);
        Assert.Equal(["Id", "Usuario"], result.Columns);
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(fixture.AuditLogService.Entries, entry =>
            entry.EntityType == "AdminSql" &&
            entry.Success &&
            entry.Details is not null &&
            entry.Details.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_UpdateReturnsAffectedRows_AndWritesAuditSummary()
    {
        await using var fixture = await SqlAdminFixture.CreateAsync();
        fixture.DbContext.UsuariosSistema.Add(CreateUser("capturista01", activo: true));
        await fixture.DbContext.SaveChangesAsync();

        var service = new SqlAdminService(fixture.DbContext, fixture.AuditLogService);

        var result = await service.ExecuteAsync(
            "UPDATE UsuariosSistema SET Activo = 0 WHERE Usuario = 'capturista01'",
            100);

        Assert.True(result.Succeeded);
        Assert.Equal("UPDATE", result.StatementType);
        Assert.Equal(1, result.AffectedRows);
        Assert.True(await fixture.DbContext.UsuariosSistema.AnyAsync(x => x.Usuario == "capturista01" && !x.Activo));
        Assert.Contains(fixture.AuditLogService.Entries, entry =>
            entry.EntityType == "AdminSql" &&
            entry.Success &&
            entry.Details is not null &&
            entry.Details.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
    }

    private static UsuarioSistema CreateUser(string username, bool activo = true)
    {
        return new UsuarioSistema
        {
            Usuario = username,
            UsuarioNormalizado = username.ToUpperInvariant(),
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rol = SecurityDefaults.AdminRole,
            Activo = activo,
            CreadoEnUtc = DateTime.UtcNow
        };
    }

    private sealed class SqlAdminFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SqlAdminFixture(
            SqliteConnection connection,
            AppDbContext dbContext,
            RecordingAuditLogService auditLogService)
        {
            _connection = connection;
            DbContext = dbContext;
            AuditLogService = auditLogService;
        }

        public AppDbContext DbContext { get; }
        public RecordingAuditLogService AuditLogService { get; }

        public static async Task<SqlAdminFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new SqlAdminFixture(connection, dbContext, new RecordingAuditLogService());
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<AuditLogEntry> Entries { get; } = [];

        public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
