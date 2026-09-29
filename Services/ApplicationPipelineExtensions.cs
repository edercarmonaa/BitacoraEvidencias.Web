using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BitacoraEvidencias.Web.Services;

public static class ApplicationPipelineExtensions
{
    public static void EnsureSqliteFolderExistsForStartup(this WebApplicationBuilder builder)
    {
        EnsureSqliteFolderExists(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            builder.Environment.ContentRootPath);
    }

    public static void UseProductionExceptionHandling(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            return;
        }

        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    public static string InitializeStorageRoot(this WebApplication app)
    {
        var storageOptions = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var storageRoot = ResolveStorageRoot(app.Environment.ContentRootPath, storageOptions.RootPath);
        ValidateLegacyStorageMigrationNotPending(app.Environment.ContentRootPath, storageRoot);
        Directory.CreateDirectory(storageRoot);
        return storageRoot;
    }

    public static void UseConfiguredHttpsRedirection(this WebApplication app, bool enableHttpsRedirection)
    {
        if (!enableHttpsRedirection)
        {
            return;
        }

        app.UseHttpsRedirection();
    }

    private static string ResolveStorageRoot(string contentRootPath, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(contentRootPath, configuredPath);
    }

    private static void ValidateLegacyStorageMigrationNotPending(string contentRootPath, string targetStorageRoot)
    {
        var legacyStorageRoot = Path.Combine(contentRootPath, "Storage");
        var legacyFullPath = Path.GetFullPath(legacyStorageRoot);
        var targetFullPath = Path.GetFullPath(targetStorageRoot);

        if (legacyFullPath.Equals(targetFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(legacyFullPath))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Se detecto almacenamiento legado en '{legacyFullPath}'. " +
            $"La ruta configurada actual es '{targetFullPath}'. " +
            "Ejecuta .\\scripts\\storage\\Migrate-LegacyStorage.ps1 antes de iniciar la aplicacion.");
    }

    private static void EnsureSqliteFolderExists(string? connectionString, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = sqliteBuilder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var absolutePath = Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.Combine(contentRootPath, dataSource);

        var folder = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }
}
