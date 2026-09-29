using System.Net;
using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Security;

public class ApplicationUserSecurityRefreshExtensionsTests
{
    [Fact]
    public async Task Middleware_Allows_Request_ForActiveUserWithoutPasswordChange()
    {
        await using var fixture = await TestApplicationFixture.CreateAsync(new UsuarioSistema
        {
            Id = 7,
            Usuario = "capturista",
            UsuarioNormalizado = "CAPTURISTA",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rol = SecurityDefaults.CapturistaRole,
            Activo = true,
            MustChangePassword = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Add("X-Test-UserId", "7");
        request.Headers.Add("X-Test-Username", "capturista");
        request.Headers.Add("X-Test-Role", SecurityDefaults.CapturistaRole);

        using var response = await fixture.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("capturista|Capturista", body);
    }

    [Fact]
    public async Task Middleware_Redirects_ToLogin_ForInactiveUser()
    {
        await using var fixture = await TestApplicationFixture.CreateAsync(new UsuarioSistema
        {
            Id = 8,
            Usuario = "inactivo",
            UsuarioNormalizado = "INACTIVO",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rol = SecurityDefaults.CapturistaRole,
            Activo = false,
            MustChangePassword = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Add("X-Test-UserId", "8");
        request.Headers.Add("X-Test-Username", "inactivo");
        request.Headers.Add("X-Test-Role", SecurityDefaults.CapturistaRole);

        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Middleware_Redirects_ToPasswordChange_WhenRequired()
    {
        await using var fixture = await TestApplicationFixture.CreateAsync(new UsuarioSistema
        {
            Id = 9,
            Usuario = "requierecambio",
            UsuarioNormalizado = "REQUIERECAMBIO",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rol = SecurityDefaults.AdminRole,
            Activo = true,
            MustChangePassword = true
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure?foo=bar");
        request.Headers.Add("X-Test-UserId", "9");
        request.Headers.Add("X-Test-Username", "requierecambio");
        request.Headers.Add("X-Test-Role", SecurityDefaults.AdminRole);

        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/ChangePassword?returnUrl=%2Fsecure%3Ffoo%3Dbar", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Middleware_Refreshes_Principal_WhenUsernameOrRoleChanges()
    {
        await using var fixture = await TestApplicationFixture.CreateAsync(new UsuarioSistema
        {
            Id = 10,
            Usuario = "usuario-actualizado",
            UsuarioNormalizado = "USUARIO-ACTUALIZADO",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rol = SecurityDefaults.AdminRole,
            Activo = true,
            MustChangePassword = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.Add("X-Test-UserId", "10");
        request.Headers.Add("X-Test-Username", "valor-viejo");
        request.Headers.Add("X-Test-Role", SecurityDefaults.CapturistaRole);

        using var response = await fixture.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("usuario-actualizado|Admin", body);
    }

    private sealed class TestApplicationFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly WebApplication _app;

        private TestApplicationFixture(SqliteConnection connection, WebApplication app, HttpClient client)
        {
            _connection = connection;
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<TestApplicationFixture> CreateAsync(UsuarioSistema user)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var contentRoot = Path.Combine(
                Path.GetTempPath(),
                "BitacoraEvidencias.Web.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRoot);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
                ContentRootPath = contentRoot
            });

            builder.WebHost.UseTestServer();
            builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestCookieAuthenticationHandler>(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    _ => { });
            builder.Services.AddAuthorization();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Database.EnsureCreatedAsync();
                dbContext.UsuariosSistema.Add(user);
                await dbContext.SaveChangesAsync();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseApplicationUserSecurityRefresh();
            app.UseAuthorization();
            app.MapGet("/secure", (HttpContext context) =>
            {
                var name = context.User.Identity?.Name ?? string.Empty;
                var role = context.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                return Results.Text($"{name}|{role}");
            });

            await app.StartAsync();

            var client = app.GetTestClient();
            client.DefaultRequestVersion = HttpVersion.Version11;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            client.BaseAddress = new Uri("http://localhost");

            return new TestApplicationFixture(connection, app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}


