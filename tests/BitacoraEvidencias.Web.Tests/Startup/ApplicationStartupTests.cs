using BitacoraEvidencias.Web.Data;
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
using Microsoft.Extensions.Options;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Startup;

public class ApplicationStartupTests
{
    [Fact]
    public void AddValidatedFileStorageOptions_Throws_ForInvalidRequestPath()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:RootPath"] = "Storage",
                ["Storage:RequestPath"] = "media",
                ["Storage:ThumbnailWidth"] = "320",
                ["Storage:ThumbnailHeight"] = "320",
                ["Storage:MaxUploadMegabytes"] = "8",
                ["Storage:MaxImageDimension"] = "2000",
                ["Storage:JpegQuality"] = "80"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddValidatedFileStorageOptions(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<FileStorageOptions>>().Value);

        Assert.Contains("Storage:RequestPath", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddValidatedFileStorageOptions_Throws_ForInvalidJpegQuality()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:RootPath"] = "Storage",
                ["Storage:RequestPath"] = "/media",
                ["Storage:ThumbnailWidth"] = "320",
                ["Storage:ThumbnailHeight"] = "320",
                ["Storage:MaxUploadMegabytes"] = "8",
                ["Storage:MaxImageDimension"] = "2000",
                ["Storage:JpegQuality"] = "101"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddValidatedFileStorageOptions(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<FileStorageOptions>>().Value);

        Assert.Contains("Storage:JpegQuality", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InitializeApplicationData_Throws_WhenResetRequestedOutsideDevelopment()
    {
        using var app = CreateApp(
            environmentName: "Production",
            connectionString: "Data Source=:memory:");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ApplicationDataStartup.InitializeApplicationData(
                app,
                configuration,
                prepareApplicationData: true,
                resetApplicationData: true));

        Assert.Contains("ResetApplicationData", exception.Message, StringComparison.Ordinal);
    }

    private static WebApplication CreateApp(string environmentName, string connectionString)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "BitacoraEvidencias.Web.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

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

        return builder.Build();
    }
}
