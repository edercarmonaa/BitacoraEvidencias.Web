using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Startup;

public class ApplicationDataStartupTests
{
    [Fact]
    public void InitializeApplicationData_PrepareMode_CreatesBootstrapAdmin_WhenDatabaseIsEmpty()
    {
        using var fixture = ApplicationDataStartupFixture.Create(environmentName: "Development");
        var configuration = fixture.CreateConfiguration(new Dictionary<string, string?>
        {
            ["Security:BootstrapAdminPassword"] = "AdminTemp123!"
        });

        ApplicationDataStartup.InitializeApplicationData(
            fixture.App,
            configuration,
            prepareApplicationData: true,
            resetApplicationData: false);

        using var scope = fixture.App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = dbContext.UsuariosSistema.Single();

        Assert.Equal("admin", admin.Usuario);
        Assert.Equal("ADMIN", admin.UsuarioNormalizado);
        Assert.True(admin.Activo);
        Assert.True(admin.MustChangePassword);
        Assert.Equal(SecurityDefaults.AdminRole, admin.Rol);
        Assert.True(PasswordHasher.VerifyPassword("AdminTemp123!", admin.PasswordHash, admin.PasswordSalt));
    }

    [Fact]
    public void InitializeApplicationData_ValidateMode_Throws_WhenDatabaseHasNoUsers()
    {
        using var fixture = ApplicationDataStartupFixture.Create(environmentName: "Production");
        fixture.EnsureCreated();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ApplicationDataStartup.InitializeApplicationData(
                fixture.App,
                fixture.CreateConfiguration(),
                prepareApplicationData: false,
                resetApplicationData: false));

        Assert.Contains("No existe ningun usuario", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeApplicationData_Throws_WhenOficioFolderNamesCollide()
    {
        using var fixture = ApplicationDataStartupFixture.Create(environmentName: "Production");
        fixture.EnsureCreated();
        fixture.SeedUser("auditor");
        fixture.SeedOficio("ABC 123");
        fixture.SeedOficio("ABC_123");

        var exception = Assert.Throws<InvalidOperationException>(
            () => ApplicationDataStartup.InitializeApplicationData(
                fixture.App,
                fixture.CreateConfiguration(),
                prepareApplicationData: false,
                resetApplicationData: false));

        Assert.Contains("colision de carpetas de oficio", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StampLegacySchemaBaseline_RegistersMigrationHistory_ForCompatibleLegacyDatabase()
    {
        using var fixture = ApplicationDataStartupFixture.Create(environmentName: "Production");
        fixture.EnsureCreated();
        fixture.SeedUser("admin");

        ApplicationDataStartup.StampLegacySchemaBaseline(
            fixture.App,
            fixture.CreateConfiguration());

        using var scope = fixture.App.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var connection = dbContext.Database.GetDbConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId = $migrationId;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$migrationId";
        parameter.Value = "20260323203540_InitialCreate";
        command.Parameters.Add(parameter);

        Assert.Equal(1L, (long)command.ExecuteScalar()!);

        ApplicationDataStartup.InitializeApplicationData(
            fixture.App,
            fixture.CreateConfiguration(),
            prepareApplicationData: false,
            resetApplicationData: false);
    }

    private sealed class ApplicationDataStartupFixture : IDisposable
    {
        private readonly string _contentRoot;
        private readonly string _connectionString;

        private ApplicationDataStartupFixture(string contentRoot, string connectionString, WebApplication app)
        {
            _contentRoot = contentRoot;
            _connectionString = connectionString;
            App = app;
        }

        public WebApplication App { get; }

        public static ApplicationDataStartupFixture Create(string environmentName)
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "BitacoraEvidencias.Web.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRoot);
            var databasePath = Path.Combine(contentRoot, "bitacora-tests.db");
            var connectionString = $"Data Source={databasePath}";

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environmentName,
                ContentRootPath = contentRoot
            });

            builder.Host.ConfigureLogging(logging => logging.ClearProviders());
            builder.Logging.ClearProviders();
            builder.Services.RemoveAll<ILoggerProvider>();
            builder.Services.RemoveAll<EventLogLoggerProvider>();
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(contentRoot, "DataProtection-Keys")));
            builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

            return new ApplicationDataStartupFixture(contentRoot, connectionString, builder.Build());
        }

        public IConfiguration CreateConfiguration(IDictionary<string, string?>? values = null)
        {
            var settings = new Dictionary<string, string?>(values ?? new Dictionary<string, string?>())
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        public void EnsureCreated()
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureCreated();
        }

        public void SeedUser(string username)
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var (hash, salt) = PasswordHasher.HashPassword("SeedPassword123!");
            dbContext.UsuariosSistema.Add(new UsuarioSistema
            {
                Usuario = username,
                UsuarioNormalizado = username.ToUpperInvariant(),
                PasswordHash = hash,
                PasswordSalt = salt,
                Rol = SecurityDefaults.AdminRole,
                Activo = true,
                MustChangePassword = false,
                CreadoEnUtc = DateTime.UtcNow
            });
            dbContext.SaveChanges();
        }

        public void SeedOficio(string numeroOficio)
        {
            using var scope = App.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Oficios.Add(new Oficio
            {
                NumeroOficio = numeroOficio,
                FechaOficio = DateTime.Today,
                Asunto = "Prueba",
                CreadoEn = DateTime.Now
            });
            dbContext.SaveChanges();
        }

        public void Dispose()
        {
            ((IAsyncDisposable)App).DisposeAsync().AsTask().GetAwaiter().GetResult();

            try
            {
                if (Directory.Exists(_contentRoot))
                {
                    Directory.Delete(_contentRoot, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}


