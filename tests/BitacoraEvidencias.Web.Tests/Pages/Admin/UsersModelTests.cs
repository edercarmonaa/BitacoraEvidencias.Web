using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Admin;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Pages.Admin;

public class UsersModelTests
{
    [Fact]
    public async Task OnPostCreateAsync_CreatesUser_WithTemporaryPasswordFlow()
    {
        await using var fixture = await UsersModelFixture.CreateAsync();
        var model = fixture.CreateModel(currentUserId: 1);
        model.NewUser = new UsersModel.CreateUserInput
        {
            Username = "captura1",
            Role = SecurityDefaults.CapturistaRole
        };

        var result = await model.OnPostCreateAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Users", redirect.PageName);

        var storedUser = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Usuario == "captura1");
        Assert.Equal("CAPTURA1", storedUser.UsuarioNormalizado);
        Assert.True(storedUser.Activo);
        Assert.True(storedUser.MustChangePassword);
        Assert.Equal(SecurityDefaults.CapturistaRole, storedUser.Rol);
        Assert.NotNull(model.FlashSuccess);
        Assert.Contains("Contrase", model.FlashSuccess!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fixture.AuditLogService.Entries, x => x.EventType == AuditEvents.Create && x.EntityId == storedUser.Id);
    }

    [Fact]
    public async Task OnPostToggleActiveAsync_Rejects_DisablingCurrentUser()
    {
        await using var fixture = await UsersModelFixture.CreateAsync();
        var currentUser = fixture.BuildUser("admin1", SecurityDefaults.AdminRole, active: true);
        fixture.DbContext.UsuariosSistema.Add(currentUser);
        await fixture.DbContext.SaveChangesAsync();

        var model = fixture.CreateModel(currentUserId: currentUser.Id);

        var result = await model.OnPostToggleActiveAsync(currentUser.Id);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Users", redirect.PageName);
        Assert.Equal("No puedes desactivar tu propio usuario.", model.FlashError);

        var storedUser = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Id == currentUser.Id);
        Assert.True(storedUser.Activo);
        Assert.Empty(fixture.AuditLogService.Entries);
    }

    [Fact]
    public async Task OnPostToggleActiveAsync_Rejects_DisablingLastActiveAdmin()
    {
        await using var fixture = await UsersModelFixture.CreateAsync();
        var currentUser = fixture.BuildUser("admin1", SecurityDefaults.AdminRole, active: true);
        var targetUser = fixture.BuildUser("admin2", SecurityDefaults.AdminRole, active: true);
        fixture.DbContext.UsuariosSistema.AddRange(currentUser, targetUser);
        await fixture.DbContext.SaveChangesAsync();

        targetUser.Activo = false;
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        var freshCurrent = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Usuario == "admin1");
        var model = fixture.CreateModel(currentUserId: 999);

        var result = await model.OnPostToggleActiveAsync(freshCurrent.Id);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Users", redirect.PageName);
        Assert.Equal("Debe existir al menos un admin activo.", model.FlashError);

        var storedUser = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Id == freshCurrent.Id);
        Assert.True(storedUser.Activo);
        Assert.Empty(fixture.AuditLogService.Entries);
    }

    [Fact]
    public async Task OnPostUpdateRoleAsync_Rejects_DemotingLastActiveAdmin()
    {
        await using var fixture = await UsersModelFixture.CreateAsync();
        var admin = fixture.BuildUser("admin1", SecurityDefaults.AdminRole, active: true);
        var inactiveAdmin = fixture.BuildUser("admin2", SecurityDefaults.AdminRole, active: false);
        fixture.DbContext.UsuariosSistema.AddRange(admin, inactiveAdmin);
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        var storedAdmin = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Usuario == "admin1");
        var model = fixture.CreateModel(currentUserId: 500);

        var result = await model.OnPostUpdateRoleAsync(storedAdmin.Id, SecurityDefaults.CapturistaRole);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Admin/Users", redirect.PageName);
        Assert.Equal("Debe existir al menos un admin activo.", model.FlashError);

        var refreshed = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Id == storedAdmin.Id);
        Assert.Equal(SecurityDefaults.AdminRole, refreshed.Rol);
        Assert.Empty(fixture.AuditLogService.Entries);
    }

    private sealed class UsersModelFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private UsersModelFixture(SqliteConnection connection, AppDbContext dbContext, RecordingAuditLogService auditLogService)
        {
            _connection = connection;
            DbContext = dbContext;
            AuditLogService = auditLogService;
        }

        public AppDbContext DbContext { get; }
        public RecordingAuditLogService AuditLogService { get; }

        public static async Task<UsersModelFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new UsersModelFixture(connection, dbContext, new RecordingAuditLogService());
        }

        public UsersModel CreateModel(int currentUserId)
        {
            var userAdministrationService = new UserAdministrationService(DbContext, AuditLogService);
            var model = new UsersModel(DbContext, userAdministrationService)
            {
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildPrincipal(currentUserId)
                    }
                }
            };

            return model;
        }

        public UsuarioSistema BuildUser(string username, string role, bool active)
        {
            var (hash, salt) = PasswordHasher.HashPassword("Temp#12345");
            return new UsuarioSistema
            {
                Usuario = username,
                UsuarioNormalizado = username.ToUpperInvariant(),
                PasswordHash = hash,
                PasswordSalt = salt,
                Rol = role,
                Activo = active,
                MustChangePassword = false,
                CreadoEnUtc = DateTime.UtcNow
            };
        }

        private static ClaimsPrincipal BuildPrincipal(int userId)
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, $"user-{userId}"),
                new Claim(ClaimTypes.Role, SecurityDefaults.AdminRole)
            ],
            "TestAuth");

            return new ClaimsPrincipal(identity);
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
