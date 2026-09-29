using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Services;

public class PhotoStorageServiceMutationTests
{
    [Fact]
    public void TryStageDeleteAndReorderCaseFolders_CanRollbackAndRestoreOriginalStructure()
    {
        using var fixture = PhotoStorageFixture.Create();
        var case001File = fixture.GetAbsolutePath("OF-001/001/original/deleted.jpg");
        var case002File = fixture.GetAbsolutePath("OF-001/002/original/moved.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(case001File)!);
        Directory.CreateDirectory(Path.GetDirectoryName(case002File)!);
        File.WriteAllBytes(case001File, [1]);
        File.WriteAllBytes(case002File, [2]);

        var staged = fixture.Service.TryStageDeleteAndReorderCaseFolders(
            "OF-001",
            1,
            [(2, 1)],
            out var mutation,
            out var errorMessage);

        Assert.True(staged);
        Assert.Null(errorMessage);
        Assert.NotNull(mutation);
        Assert.False(Directory.Exists(fixture.GetAbsolutePath("OF-001/002")));
        Assert.True(Directory.Exists(fixture.GetAbsolutePath("OF-001/001")));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(fixture.GetAbsolutePath("OF-001/001/original/moved.jpg")));

        var rolledBack = fixture.Service.TryRollbackStagedCaseFolderMutation(mutation, out var rollbackError);

        Assert.True(rolledBack);
        Assert.Null(rollbackError);
        Assert.True(File.Exists(case001File));
        Assert.True(File.Exists(case002File));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(case001File));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(case002File));
    }

    [Fact]
    public void TryStageDeleteAndReorderCaseFolders_FinalizeDeletesStagedDeletedCase()
    {
        using var fixture = PhotoStorageFixture.Create();
        var case001File = fixture.GetAbsolutePath("OF-001/001/original/deleted.jpg");
        var case002File = fixture.GetAbsolutePath("OF-001/002/original/moved.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(case001File)!);
        Directory.CreateDirectory(Path.GetDirectoryName(case002File)!);
        File.WriteAllBytes(case001File, [1]);
        File.WriteAllBytes(case002File, [2]);

        var staged = fixture.Service.TryStageDeleteAndReorderCaseFolders(
            "OF-001",
            1,
            [(2, 1)],
            out var mutation,
            out var errorMessage);

        Assert.True(staged);
        Assert.Null(errorMessage);
        Assert.NotNull(mutation);
        Assert.NotNull(mutation!.StagedDeletedCasePath);
        Assert.True(Directory.Exists(mutation.StagedDeletedCasePath!));

        var finalized = fixture.Service.TryFinalizeStagedCaseFolderMutation(mutation, out var finalizeError);

        Assert.True(finalized);
        Assert.Null(finalizeError);
        Assert.False(Directory.Exists(mutation.StagedDeletedCasePath!));
        Assert.False(Directory.Exists(fixture.GetAbsolutePath("OF-001/002")));
        Assert.True(File.Exists(fixture.GetAbsolutePath("OF-001/001/original/moved.jpg")));
    }

    private sealed class PhotoStorageFixture : IDisposable
    {
        private readonly string _root;

        private PhotoStorageFixture(string root, PhotoStorageService service)
        {
            _root = root;
            Service = service;
        }

        public PhotoStorageService Service { get; }

        public static PhotoStorageFixture Create()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "BitacoraEvidencias.Web.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRoot);

            var environment = new TestWebHostEnvironment
            {
                ApplicationName = "BitacoraEvidencias.Web.Tests",
                ContentRootPath = contentRoot,
                ContentRootFileProvider = new PhysicalFileProvider(contentRoot),
                EnvironmentName = "Development",
                WebRootPath = contentRoot,
                WebRootFileProvider = new PhysicalFileProvider(contentRoot)
            };

            var options = Options.Create(new FileStorageOptions
            {
                RootPath = "Storage",
                RequestPath = "/media"
            });

            return new PhotoStorageFixture(
                contentRoot,
                new PhotoStorageService(
                    environment,
                    options,
                    new BitacoraEvidencias.Web.Security.SensitiveDataProtectionService(
                        Options.Create(new BitacoraEvidencias.Web.Security.SensitiveDataProtectionOptions()))));
        }

        public string GetAbsolutePath(string relativePath)
        {
            return Path.Combine(_root, "Storage", relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
