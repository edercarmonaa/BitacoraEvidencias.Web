using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Services;

public class PhotoStorageServiceTests
{
    [Fact]
    public void TryOpenReadEvidence_ReturnsFalse_ForPathTraversal()
    {
        using var fixture = PhotoStorageFixture.Create();

        var result = fixture.Service.TryOpenReadEvidence("../outside.txt", out var stream, out var contentType);

        Assert.False(result);
        Assert.Null(stream);
        Assert.Equal("application/octet-stream", contentType);
    }

    [Fact]
    public void TryOpenReadEvidence_ReturnsStreamAndContentType_ForValidFile()
    {
        using var fixture = PhotoStorageFixture.Create();
        var relativePath = "OF-001/001/original/test.jpg";
        var absolutePath = fixture.GetAbsolutePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllBytes(absolutePath, [1, 2, 3, 4]);

        var result = fixture.Service.TryOpenReadEvidence(relativePath, out var stream, out var contentType);

        Assert.True(result);
        Assert.NotNull(stream);
        Assert.Equal("image/jpeg", contentType);

        using (stream)
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, memory.ToArray());
        }
    }

    [Fact]
    public void DeleteEvidenceFiles_RemovesFiles_AndCleansEmptyDirectories()
    {
        using var fixture = PhotoStorageFixture.Create();
        var originalRelative = "OF-001/001/original/a.jpg";
        var thumbnailRelative = "OF-001/001/thumb/a.jpg";

        var originalAbsolute = fixture.GetAbsolutePath(originalRelative);
        var thumbnailAbsolute = fixture.GetAbsolutePath(thumbnailRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(originalAbsolute)!);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailAbsolute)!);
        File.WriteAllBytes(originalAbsolute, [1]);
        File.WriteAllBytes(thumbnailAbsolute, [2]);

        fixture.Service.DeleteEvidenceFiles(originalRelative, thumbnailRelative);

        Assert.False(File.Exists(originalAbsolute));
        Assert.False(File.Exists(thumbnailAbsolute));
        Assert.False(Directory.Exists(Path.GetDirectoryName(originalAbsolute)!));
        Assert.False(Directory.Exists(Path.GetDirectoryName(thumbnailAbsolute)!));
    }

    [Theory]
    [InlineData("OF-001/001/original/a.jpg", "OF-001", "OF-999", "OF-999/001/original/a.jpg")]
    [InlineData("OF-001/001/original/a.jpg", "OF-XYZ", "OF-999", "OF-001/001/original/a.jpg")]
    public void RebaseRelativePathForOficio_UpdatesOnlyMatchingPaths(
        string relativePath,
        string oldNumeroOficio,
        string newNumeroOficio,
        string expected)
    {
        using var fixture = PhotoStorageFixture.Create();

        var result = fixture.Service.RebaseRelativePathForOficio(relativePath, oldNumeroOficio, newNumeroOficio);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("OF-001/001/original/a.jpg", "OF-001", 1, 3, "OF-001/003/original/a.jpg")]
    [InlineData("OF-001/002/original/a.jpg", "OF-001", 1, 3, "OF-001/002/original/a.jpg")]
    public void RebaseRelativePathForCase_UpdatesOnlyMatchingCaseFolder(
        string relativePath,
        string numeroOficio,
        int oldConsecutivo,
        int newConsecutivo,
        string expected)
    {
        using var fixture = PhotoStorageFixture.Create();

        var result = fixture.Service.RebaseRelativePathForCase(relativePath, numeroOficio, oldConsecutivo, newConsecutivo);

        Assert.Equal(expected, result);
    }


    [Fact]
    public void TryStageDeleteEvidenceFiles_StagesFiles_AndFinalizeDeletesThem()
    {
        using var fixture = PhotoStorageFixture.Create();
        var originalRelative = "OF-001/001/original/a.jpg";
        var thumbnailRelative = "OF-001/001/thumb/a.jpg";

        var originalAbsolute = fixture.GetAbsolutePath(originalRelative);
        var thumbnailAbsolute = fixture.GetAbsolutePath(thumbnailRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(originalAbsolute)!);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailAbsolute)!);
        File.WriteAllBytes(originalAbsolute, [1]);
        File.WriteAllBytes(thumbnailAbsolute, [2]);

        var staged = fixture.Service.TryStageDeleteEvidenceFiles(
            originalRelative,
            thumbnailRelative,
            out var mutation,
            out var errorMessage);

        Assert.True(staged);
        Assert.Null(errorMessage);
        Assert.NotNull(mutation);
        Assert.False(File.Exists(originalAbsolute));
        Assert.False(File.Exists(thumbnailAbsolute));
        Assert.All(mutation!.Files, item => Assert.True(File.Exists(item.StagedPath)));

        var finalized = fixture.Service.TryFinalizeStagedEvidenceDeletion(mutation, out var finalizeError);

        Assert.True(finalized);
        Assert.Null(finalizeError);
        Assert.All(mutation.Files, item => Assert.False(File.Exists(item.StagedPath)));
    }

    [Fact]
    public void TryStageDeleteEvidenceFiles_CanRollbackAndRestoreOriginalFiles()
    {
        using var fixture = PhotoStorageFixture.Create();
        var originalRelative = "OF-001/001/original/a.jpg";
        var thumbnailRelative = "OF-001/001/thumb/a.jpg";

        var originalAbsolute = fixture.GetAbsolutePath(originalRelative);
        var thumbnailAbsolute = fixture.GetAbsolutePath(thumbnailRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(originalAbsolute)!);
        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailAbsolute)!);
        File.WriteAllBytes(originalAbsolute, [10]);
        File.WriteAllBytes(thumbnailAbsolute, [20]);

        var staged = fixture.Service.TryStageDeleteEvidenceFiles(
            originalRelative,
            thumbnailRelative,
            out var mutation,
            out var errorMessage);

        Assert.True(staged);
        Assert.Null(errorMessage);
        Assert.NotNull(mutation);

        var rolledBack = fixture.Service.TryRollbackStagedEvidenceDeletion(mutation, out var rollbackError);

        Assert.True(rolledBack);
        Assert.Null(rollbackError);
        Assert.True(File.Exists(originalAbsolute));
        Assert.True(File.Exists(thumbnailAbsolute));
        Assert.Equal(new byte[] { 10 }, File.ReadAllBytes(originalAbsolute));
        Assert.Equal(new byte[] { 20 }, File.ReadAllBytes(thumbnailAbsolute));
    }

    [Fact]
    public void TryStageDeleteOficioFolder_CanRollbackAndRestoreFolder()
    {
        using var fixture = PhotoStorageFixture.Create();
        var oficioFile = fixture.GetAbsolutePath("OF-001/001/original/a.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(oficioFile)!);
        File.WriteAllBytes(oficioFile, [7]);

        var staged = fixture.Service.TryStageDeleteOficioFolder("OF-001", out var mutation, out var errorMessage);

        Assert.True(staged);
        Assert.Null(errorMessage);
        Assert.NotNull(mutation);
        Assert.False(Directory.Exists(mutation!.OriginalPath));
        Assert.True(Directory.Exists(mutation.StagedPath));

        var rolledBack = fixture.Service.TryRollbackStagedOficioFolderDeletion(mutation, out var rollbackError);

        Assert.True(rolledBack);
        Assert.Null(rollbackError);
        Assert.True(Directory.Exists(mutation.OriginalPath));
        Assert.False(Directory.Exists(mutation.StagedPath));
        Assert.True(File.Exists(oficioFile));
    }

    [Fact]
    public void TryStageDeleteOficioFolder_FinalizeDeletesStagedFolder()
    {
        using var fixture = PhotoStorageFixture.Create();
        var oficioFile = fixture.GetAbsolutePath("OF-001/001/original/a.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(oficioFile)!);
        File.WriteAllBytes(oficioFile, [7]);

        var staged = fixture.Service.TryStageDeleteOficioFolder("OF-001", out var mutation, out var errorMessage);

        Assert.True(staged);
        Assert.Null(errorMessage);
        Assert.NotNull(mutation);

        var finalized = fixture.Service.TryFinalizeStagedOficioFolderDeletion(mutation, out var finalizeError);

        Assert.True(finalized);
        Assert.Null(finalizeError);
        Assert.False(Directory.Exists(mutation!.OriginalPath));
        Assert.False(Directory.Exists(mutation.StagedPath));
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
