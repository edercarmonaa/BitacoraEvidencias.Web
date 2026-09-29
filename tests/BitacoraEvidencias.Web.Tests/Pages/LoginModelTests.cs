using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Pages;

public class LoginModelTests
{
    [Fact]
    public async Task OnPostAsync_InvalidPassword_IncrementsFailedAttempts_AndReturnsPage()
    {
        var user = BuildUser("capturista", "Correct#123");
        await using var fixture = await LoginTestFixture.CreateAsync(user);

        var model = fixture.CreateModel();
        model.Input = new LoginModel.InputModel
        {
            Usuario = "capturista",
            Contrasena = "Wrong#123"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Usuario o contraseña incorrectos.", model.ErrorMessage);

        var storedUser = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(1, storedUser.IntentosFallidos);
        Assert.Null(storedUser.BloqueadoHastaUtc);
        Assert.Contains(fixture.AuditLogService.Entries, x => x.EventType == AuditEvents.LoginFail);
    }

    [Fact]
    public async Task OnPostAsync_LastInvalidPassword_BlocksUser_AndWritesLockoutAudit()
    {
        var user = BuildUser("admin", "Correct#123");
        user.IntentosFallidos = SecurityDefaults.MaxFailedAttempts - 1;

        await using var fixture = await LoginTestFixture.CreateAsync(user);

        var model = fixture.CreateModel();
        model.Input = new LoginModel.InputModel
        {
            Usuario = "admin",
            Contrasena = "StillWrong#123"
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Usuario o contraseña incorrectos.", model.ErrorMessage);

        var storedUser = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(0, storedUser.IntentosFallidos);
        Assert.True(storedUser.BloqueadoHastaUtc.HasValue);
        Assert.True(storedUser.BloqueadoHastaUtc.Value > DateTime.UtcNow);
        Assert.Contains(fixture.AuditLogService.Entries, x => x.EventType == AuditEvents.LoginFail);
        Assert.Contains(fixture.AuditLogService.Entries, x => x.EventType == AuditEvents.Lockout);
    }

    [Fact]
    public async Task OnPostAsync_ValidPassword_SignsIn_ResetsSecurityState_AndRedirectsLocally()
    {
        var user = BuildUser("capturista", "Correct#123");
        user.IntentosFallidos = 2;
        user.BloqueadoHastaUtc = DateTime.UtcNow.AddMinutes(-5);

        await using var fixture = await LoginTestFixture.CreateAsync(user);

        var model = fixture.CreateModel();
        model.Input = new LoginModel.InputModel
        {
            Usuario = "capturista",
            Contrasena = "Correct#123"
        };

        var result = await model.OnPostAsync("/busqueda");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/busqueda", redirect.Url);

        var storedUser = await fixture.DbContext.UsuariosSistema.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(0, storedUser.IntentosFallidos);
        Assert.Null(storedUser.BloqueadoHastaUtc);
        Assert.True(storedUser.UltimoAccesoUtc.HasValue);

        Assert.True(fixture.AuthenticationService.SignInCalled);
        Assert.NotNull(fixture.AuthenticationService.LastPrincipal);
        Assert.Equal("capturista", fixture.AuthenticationService.LastPrincipal?.Identity?.Name);
        Assert.Contains(fixture.AuditLogService.Entries, x => x.EventType == AuditEvents.Login);
    }

    private static UsuarioSistema BuildUser(string username, string password)
    {
        var (hash, salt) = PasswordHasher.HashPassword(password);
        return new UsuarioSistema
        {
            Id = Random.Shared.Next(1, 100000),
            Usuario = username,
            UsuarioNormalizado = username.ToUpperInvariant(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Rol = SecurityDefaults.CapturistaRole,
            Activo = true,
            MustChangePassword = false,
            CreadoEnUtc = DateTime.UtcNow
        };
    }

    private sealed class LoginTestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        private LoginTestFixture(
            SqliteConnection connection,
            AppDbContext dbContext,
            RecordingAuditLogService auditLogService,
            RecordingAuthenticationService authenticationService,
            ServiceProvider serviceProvider)
        {
            _connection = connection;
            DbContext = dbContext;
            AuditLogService = auditLogService;
            AuthenticationService = authenticationService;
            _serviceProvider = serviceProvider;
        }

        public AppDbContext DbContext { get; }
        public RecordingAuditLogService AuditLogService { get; }
        public RecordingAuthenticationService AuthenticationService { get; }

        public static async Task<LoginTestFixture> CreateAsync(UsuarioSistema user)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.UsuariosSistema.Add(user);
            await dbContext.SaveChangesAsync();

            var authenticationService = new RecordingAuthenticationService();
            var services = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(authenticationService)
                .BuildServiceProvider();

            return new LoginTestFixture(
                connection,
                dbContext,
                new RecordingAuditLogService(),
                authenticationService,
                services);
        }

        public LoginModel CreateModel()
        {
            var model = new LoginModel(DbContext, AuditLogService)
            {
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        RequestServices = _serviceProvider
                    },
                    RouteData = new RouteData()
                },
                Url = new TestUrlHelper()
            };

            return model;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
            await _serviceProvider.DisposeAsync();
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

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public bool SignInCalled { get; private set; }
        public ClaimsPrincipal? LastPrincipal { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignInCalled = true;
            LastPrincipal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }

    private sealed class TestUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();

        public string? Action(UrlActionContext actionContext) => null;

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url)
        {
            return !string.IsNullOrWhiteSpace(url) &&
                   (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal));
        }

        public string? Link(string? routeName, object? values) => null;

        public string? RouteUrl(UrlRouteContext routeContext) => null;
    }
}
