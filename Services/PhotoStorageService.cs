using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Text;
using BitacoraEvidencias.Web.Security;
using Microsoft.Extensions.Options;

namespace BitacoraEvidencias.Web.Services;

public class PhotoStorageService(
    IWebHostEnvironment environment,
    IOptions<FileStorageOptions> options,
    ISensitiveDataProtectionService sensitiveDataProtection) : IPhotoStorageService
{
    private const int ExifOrientationPropertyId = 0x0112;
    private const int MaxImageSidePixels = 5000;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff"
    };

    private readonly FileStorageOptions _options = options.Value;

    public Task<StoredPhotoInfo> SaveEvidenceAsync(
        IFormFile file,
        string numeroOficio,
        int consecutivo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        return SaveEvidenceCoreAsync(
            file.FileName,
            file.Length,
            numeroOficio,
            consecutivo,
            file.OpenReadStream,
            cancellationToken);
    }

    public Task<StoredPhotoInfo> SaveEvidenceFromPathAsync(
        string sourceFilePath,
        string numeroOficio,
        int consecutivo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new InvalidOperationException("La ruta del archivo es obligatoria.");
        }

        string absolutePath;
        try
        {
            absolutePath = Path.GetFullPath(sourceFilePath);
        }
        catch
        {
            throw new InvalidOperationException("La ruta del archivo no es valida.");
        }

        FileInfo sourceFileInfo;
        try
        {
            sourceFileInfo = new FileInfo(absolutePath);
        }
        catch
        {
            throw new InvalidOperationException("La ruta del archivo no es valida.");
        }

        if (!sourceFileInfo.Exists)
        {
            throw new InvalidOperationException("El archivo no existe en la ruta indicada.");
        }

        return SaveEvidenceCoreAsync(
            sourceFileInfo.Name,
            sourceFileInfo.Length,
            numeroOficio,
            consecutivo,
            () => new FileStream(sourceFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read),
            cancellationToken);
    }

    public bool TryValidateSourceEvidenceFile(string sourceFilePath, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            errorMessage = "ARCHIVO es obligatorio.";
            return false;
        }

        string absolutePath;
        try
        {
            absolutePath = Path.GetFullPath(sourceFilePath);
        }
        catch
        {
            errorMessage = "ARCHIVO no contiene una ruta valida.";
            return false;
        }

        FileInfo sourceFileInfo;
        try
        {
            sourceFileInfo = new FileInfo(absolutePath);
        }
        catch
        {
            errorMessage = "ARCHIVO no contiene una ruta valida.";
            return false;
        }

        if (!sourceFileInfo.Exists)
        {
            errorMessage = "El archivo no existe en la ruta indicada.";
            return false;
        }

        try
        {
            ValidateSourceBasics(sourceFileInfo.Name, sourceFileInfo.Length);
            ValidateImagePayload(() => new FileStream(sourceFileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read));
            return true;
        }
        catch (InvalidOperationException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        catch
        {
            errorMessage = "No fue posible leer el archivo indicado.";
            return false;
        }
    }

    private async Task<StoredPhotoInfo> SaveEvidenceCoreAsync(
        string sourceFileName,
        long sourceLength,
        string numeroOficio,
        int consecutivo,
        Func<Stream> openReadStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateSourceBasics(sourceFileName, sourceLength);

        var extension = Path.GetExtension(sourceFileName);
        var oficioFolder = GetOficioFolderName(numeroOficio);
        var caseFolder = consecutivo.ToString("000");
        var storageRoot = ResolveRootPath(environment);
        var originalFolder = Path.Combine(storageRoot, oficioFolder, caseFolder, "original");
        var thumbFolder = Path.Combine(storageRoot, oficioFolder, caseFolder, "thumb");

        Directory.CreateDirectory(originalFolder);
        Directory.CreateDirectory(thumbFolder);

        var normalizedExtension = extension.ToLowerInvariant();
        var outputExtension = OperatingSystem.IsWindows() ? ".jpg" : normalizedExtension;
        var baseName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        var originalName = $"{baseName}{outputExtension}";
        var thumbName = $"{baseName}{outputExtension}";

        var originalAbsolutePath = Path.Combine(originalFolder, originalName);
        var thumbAbsolutePath = Path.Combine(thumbFolder, thumbName);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                ProcessAndSaveImageWindows(
                    openReadStream,
                    originalAbsolutePath,
                    thumbAbsolutePath,
                    cancellationToken);
            }
            else
            {
                await ProcessAndSaveImagePortableAsync(
                    openReadStream,
                    originalAbsolutePath,
                    thumbAbsolutePath,
                    cancellationToken);
            }

            ProtectEvidenceFileInPlace(originalAbsolutePath);
            ProtectEvidenceFileInPlace(thumbAbsolutePath);
        }
        catch (InvalidOperationException)
        {
            CleanupGeneratedFiles(originalAbsolutePath, thumbAbsolutePath);
            throw;
        }
        catch (Exception)
        {
            CleanupGeneratedFiles(originalAbsolutePath, thumbAbsolutePath);
            throw new InvalidOperationException("No fue posible procesar la imagen. Verifica que sea un archivo de imagen valido.");
        }

        var relativeOriginal = NormalizeRelative(Path.Combine(oficioFolder, caseFolder, "original", originalName));
        var relativeThumb = NormalizeRelative(Path.Combine(oficioFolder, caseFolder, "thumb", thumbName));

        return new StoredPhotoInfo
        {
            RelativeFilePath = relativeOriginal,
            RelativeThumbnailPath = relativeThumb
        };
    }

    private void ValidateSourceBasics(string sourceFileName, long sourceLength)
    {
        if (sourceLength <= 0)
        {
            throw new InvalidOperationException("El archivo esta vacio.");
        }

        var maxUploadBytes = GetMaxUploadBytes();
        if (sourceLength > maxUploadBytes)
        {
            throw new InvalidOperationException($"El archivo excede el limite permitido de {_options.MaxUploadMegabytes} MB.");
        }

        var extension = Path.GetExtension(sourceFileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Formato no permitido. Usa JPG, PNG, BMP, GIF o TIFF.");
        }
    }

    private void ValidateImagePayload(Func<Stream> openReadStream)
    {
        if (OperatingSystem.IsWindows())
        {
            ValidateImagePayloadWindows(openReadStream);
            return;
        }

        using var inputStreamPortable = openReadStream();
        if (!TryReadImageDimensions(inputStreamPortable, out var width, out var height))
        {
            throw new InvalidOperationException("No fue posible procesar la imagen. Verifica que sea un archivo de imagen valido.");
        }

        ThrowIfImageExceedsMaxDimensions(width, height);
    }

    [SupportedOSPlatform("windows")]
    private void ValidateImagePayloadWindows(Func<Stream> openReadStream)
    {
        using var inputStream = openReadStream();
        using var sourceImage = Image.FromStream(inputStream, useEmbeddedColorManagement: true, validateImageData: true);
        ThrowIfImageExceedsMaxDimensions(sourceImage);
    }

    [SupportedOSPlatform("windows")]
    private void ProcessAndSaveImageWindows(
        Func<Stream> openReadStream,
        string originalAbsolutePath,
        string thumbAbsolutePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var inputStream = openReadStream();
        using var sourceImage = Image.FromStream(inputStream, useEmbeddedColorManagement: true, validateImageData: true);
        ThrowIfImageExceedsMaxDimensions(sourceImage);

        ApplyExifOrientation(sourceImage);

        using var optimizedImage = CreateOptimizedImage(sourceImage, _options.MaxImageDimension);
        SaveJpegWithQuality(optimizedImage, originalAbsolutePath, _options.JpegQuality);
        CreateThumbnailSafe(originalAbsolutePath, thumbAbsolutePath);
    }

    private async Task ProcessAndSaveImagePortableAsync(
        Func<Stream> openReadStream,
        string originalAbsolutePath,
        string thumbAbsolutePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using (var inspectionStream = openReadStream())
        {
            if (!TryReadImageDimensions(inspectionStream, out var width, out var height))
            {
                throw new InvalidOperationException("No fue posible procesar la imagen. Verifica que sea un archivo de imagen valido.");
            }

            ThrowIfImageExceedsMaxDimensions(width, height);
        }

        await using (var inputStream = openReadStream())
        await using (var outputStream = new FileStream(originalAbsolutePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await inputStream.CopyToAsync(outputStream, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        CreateThumbnailSafe(originalAbsolutePath, thumbAbsolutePath);
    }

    private long GetMaxUploadBytes()
    {
        var megabytes = Math.Max(1, _options.MaxUploadMegabytes);
        return megabytes * 1024L * 1024L;
    }

    [SupportedOSPlatform("windows")]
    private static void ThrowIfImageExceedsMaxDimensions(Image image)
    {
        ThrowIfImageExceedsMaxDimensions(image.Width, image.Height);
    }

    private static void ThrowIfImageExceedsMaxDimensions(int width, int height)
    {
        if (width <= MaxImageSidePixels && height <= MaxImageSidePixels)
        {
            return;
        }

        throw new InvalidOperationException(
            $"La imagen excede el tamano maximo permitido de {MaxImageSidePixels} px por lado.");
    }

    private static void CleanupGeneratedFiles(params string[] absolutePaths)
    {
        foreach (var path in absolutePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    public bool TryOpenReadEvidence(string relativePath, out Stream? stream, out string contentType)
    {
        stream = null;
        contentType = "application/octet-stream";

        if (!TryResolveSafeAbsolutePath(relativePath, out var absolutePath))
        {
            return false;
        }

        if (!File.Exists(absolutePath))
        {
            return false;
        }

        try
        {
            var fileBytes = File.ReadAllBytes(absolutePath);
            if (sensitiveDataProtection.IsConfigured && sensitiveDataProtection.IsProtectedPayload(fileBytes))
            {
                stream = new MemoryStream(sensitiveDataProtection.UnprotectBytes(fileBytes));
            }
            else
            {
                stream = new MemoryStream(fileBytes);
            }

            contentType = ResolveImageContentType(absolutePath);
            return true;
        }
        catch
        {
            stream?.Dispose();
            stream = null;
            return false;
        }
    }

    public bool TryProtectExistingEvidenceFile(string relativePath, out string? errorMessage)
    {
        errorMessage = null;
        if (!TryResolveSafeAbsolutePath(relativePath, out var absolutePath))
        {
            errorMessage = "La ruta de evidencia no es valida.";
            return false;
        }

        if (!File.Exists(absolutePath))
        {
            return true;
        }

        try
        {
            ProtectEvidenceFileInPlace(absolutePath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryProtectExistingOficioFolder(string numeroOficio, out string? errorMessage)
    {
        errorMessage = null;
        if (!sensitiveDataProtection.IsConfigured)
        {
            return true;
        }

        var legacyPath = Path.Combine(StorageRootPath, OficioStoragePath.ToFolderName(numeroOficio));
        var protectedPath = GetOficioFolderPath(numeroOficio);
        if (legacyPath.Equals(protectedPath, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(legacyPath))
        {
            return true;
        }

        try
        {
            if (Directory.Exists(protectedPath))
            {
                errorMessage = $"Ya existe la carpeta protegida '{protectedPath}'.";
                return false;
            }

            var protectedParent = Path.GetDirectoryName(protectedPath);
            if (!string.IsNullOrWhiteSpace(protectedParent))
            {
                Directory.CreateDirectory(protectedParent);
            }

            Directory.Move(legacyPath, protectedPath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public void DeleteEvidenceFiles(string? relativeFilePath, string? relativeThumbnailPath)
    {
        var directoriesToClean = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var deletedOriginal = DeleteRelativeFile(relativeFilePath);
        if (!string.IsNullOrWhiteSpace(deletedOriginal))
        {
            var originalFolder = Path.GetDirectoryName(deletedOriginal);
            if (!string.IsNullOrWhiteSpace(originalFolder))
            {
                directoriesToClean.Add(originalFolder);
            }
        }

        var deletedThumbnail = DeleteRelativeFile(relativeThumbnailPath);
        if (!string.IsNullOrWhiteSpace(deletedThumbnail))
        {
            var thumbnailFolder = Path.GetDirectoryName(deletedThumbnail);
            if (!string.IsNullOrWhiteSpace(thumbnailFolder))
            {
                directoriesToClean.Add(thumbnailFolder);
            }
        }

        foreach (var directory in directoriesToClean)
        {
            DeleteEmptyDirectoriesUpToRoot(directory);
        }
    }

    public void DeleteCaseFolder(string numeroOficio, int consecutivo)
    {
        var casePath = Path.Combine(GetOficioFolderPath(numeroOficio), consecutivo.ToString("000"));
        if (Directory.Exists(casePath))
        {
            Directory.Delete(casePath, recursive: true);
        }

        var oficioPath = Path.GetDirectoryName(casePath);
        if (!string.IsNullOrWhiteSpace(oficioPath))
        {
            DeleteEmptyDirectoriesUpToRoot(oficioPath);
        }
    }

    public void DeleteOficioFolder(string numeroOficio)
    {
        var oficioPath = GetOficioFolderPath(numeroOficio);
        if (Directory.Exists(oficioPath))
        {
            Directory.Delete(oficioPath, recursive: true);
        }
    }

    public bool TryStageDeleteEvidenceFiles(
        string? relativeFilePath,
        string? relativeThumbnailPath,
        out StagedEvidenceDeletion? mutation,
        out string? errorMessage)
    {
        mutation = new StagedEvidenceDeletion();
        errorMessage = null;

        try
        {
            StageEvidenceFile(relativeFilePath, mutation);
            StageEvidenceFile(relativeThumbnailPath, mutation);
            return true;
        }
        catch (Exception ex)
        {
            TryRollbackStagedEvidenceDeletion(mutation, out _);
            mutation = null;
            errorMessage = $"No fue posible preparar la eliminacion fisica de la evidencia: {ex.Message}";
            return false;
        }
    }

    public bool TryRollbackStagedEvidenceDeletion(StagedEvidenceDeletion? mutation, out string? errorMessage)
    {
        errorMessage = null;
        if (mutation is null)
        {
            return true;
        }

        try
        {
            foreach (var item in mutation.Files.AsEnumerable().Reverse())
            {
                if (!File.Exists(item.StagedPath))
                {
                    continue;
                }

                var originalFolder = Path.GetDirectoryName(item.OriginalPath);
                if (!string.IsNullOrWhiteSpace(originalFolder))
                {
                    Directory.CreateDirectory(originalFolder);
                }

                if (File.Exists(item.OriginalPath))
                {
                    File.Delete(item.OriginalPath);
                }

                File.Move(item.StagedPath, item.OriginalPath);
            }

            CleanupStagedDeleteDirectories(mutation.Files.Select(x => x.StagedPath));
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"No fue posible revertir la eliminacion fisica de la evidencia: {ex.Message}";
            return false;
        }
    }

    public bool TryFinalizeStagedEvidenceDeletion(StagedEvidenceDeletion? mutation, out string? errorMessage)
    {
        errorMessage = null;
        if (mutation is null)
        {
            return true;
        }

        try
        {
            foreach (var item in mutation.Files)
            {
                if (File.Exists(item.StagedPath))
                {
                    File.Delete(item.StagedPath);
                }

                var originalFolder = Path.GetDirectoryName(item.OriginalPath);
                if (!string.IsNullOrWhiteSpace(originalFolder))
                {
                    DeleteEmptyDirectoriesUpToRoot(originalFolder);
                }
            }

            CleanupStagedDeleteDirectories(mutation.Files.Select(x => x.StagedPath));
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"La evidencia se elimino de la base, pero no fue posible completar la limpieza fisica: {ex.Message}";
            return false;
        }
    }

    public bool TryStageDeleteOficioFolder(string numeroOficio, out StagedOficioFolderDeletion? mutation, out string? errorMessage)
    {
        mutation = null;
        errorMessage = null;

        var oficioPath = GetOficioFolderPath(numeroOficio);
        if (!Directory.Exists(oficioPath))
        {
            return true;
        }

        try
        {
            var stagedPath = Path.Combine(
                GetStagedDeleteRootPath(),
                "oficios",
                $"__delete_{GetOficioFolderName(numeroOficio)}_{Guid.NewGuid():N}");

            var stagedFolder = Path.GetDirectoryName(stagedPath);
            if (!string.IsNullOrWhiteSpace(stagedFolder))
            {
                Directory.CreateDirectory(stagedFolder);
            }

            Directory.Move(oficioPath, stagedPath);
            mutation = new StagedOficioFolderDeletion
            {
                OriginalPath = oficioPath,
                StagedPath = stagedPath
            };

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"No fue posible preparar la eliminacion fisica del oficio: {ex.Message}";
            return false;
        }
    }

    public bool TryRollbackStagedOficioFolderDeletion(StagedOficioFolderDeletion? mutation, out string? errorMessage)
    {
        errorMessage = null;
        if (mutation is null)
        {
            return true;
        }

        try
        {
            if (!Directory.Exists(mutation.StagedPath))
            {
                return true;
            }

            var originalParent = Path.GetDirectoryName(mutation.OriginalPath);
            if (!string.IsNullOrWhiteSpace(originalParent))
            {
                Directory.CreateDirectory(originalParent);
            }

            if (Directory.Exists(mutation.OriginalPath))
            {
                Directory.Delete(mutation.OriginalPath, recursive: true);
            }

            Directory.Move(mutation.StagedPath, mutation.OriginalPath);
            CleanupStagedDeleteDirectories([mutation.StagedPath]);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"No fue posible revertir la eliminacion fisica del oficio: {ex.Message}";
            return false;
        }
    }

    public bool TryFinalizeStagedOficioFolderDeletion(StagedOficioFolderDeletion? mutation, out string? errorMessage)
    {
        errorMessage = null;
        if (mutation is null)
        {
            return true;
        }

        try
        {
            if (Directory.Exists(mutation.StagedPath))
            {
                Directory.Delete(mutation.StagedPath, recursive: true);
            }

            CleanupStagedDeleteDirectories([mutation.StagedPath]);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"El oficio se elimino de la base, pero no fue posible completar la limpieza fisica: {ex.Message}";
            return false;
        }
    }

    public bool TryStageDeleteAndReorderCaseFolders(
        string numeroOficio,
        int deletedConsecutivo,
        IReadOnlyCollection<(int OldConsecutivo, int NewConsecutivo)> moves,
        out StagedCaseFolderMutation? mutation,
        out string? errorMessage)
    {
        errorMessage = null;
        mutation = new StagedCaseFolderMutation
        {
            NumeroOficio = numeroOficio
        };

        var oficioPath = GetOficioFolderPath(numeroOficio);
        if (!Directory.Exists(oficioPath))
        {
            return true;
        }

        try
        {
            var deletedCasePath = Path.Combine(oficioPath, deletedConsecutivo.ToString("000"));
            if (Directory.Exists(deletedCasePath))
            {
                var stagedDeletedPath = Path.Combine(oficioPath, $"__delete_{deletedConsecutivo:000}_{Guid.NewGuid():N}");
                Directory.Move(deletedCasePath, stagedDeletedPath);
                mutation.OriginalDeletedCasePath = deletedCasePath;
                mutation.StagedDeletedCasePath = stagedDeletedPath;
            }

            foreach (var move in moves.Where(x => x.OldConsecutivo != x.NewConsecutivo).OrderBy(x => x.OldConsecutivo))
            {
                var oldPath = Path.Combine(oficioPath, move.OldConsecutivo.ToString("000"));
                if (!Directory.Exists(oldPath))
                {
                    continue;
                }

                var tempPath = Path.Combine(oficioPath, $"__tmp_{move.OldConsecutivo:000}_{Guid.NewGuid():N}");
                Directory.Move(oldPath, tempPath);
                mutation.ReorderedFolders.Add(new StagedCaseFolderMove
                {
                    OriginalPath = oldPath,
                    TempPath = tempPath,
                    NewPath = Path.Combine(oficioPath, move.NewConsecutivo.ToString("000")),
                    CurrentPath = tempPath
                });
            }

            foreach (var stagedMove in mutation.ReorderedFolders)
            {
                if (Directory.Exists(stagedMove.NewPath))
                {
                    errorMessage = $"No se puede completar el reordenado; ya existe la carpeta destino {Path.GetFileName(stagedMove.NewPath)}.";
                    TryRollbackStagedCaseFolderMutation(mutation, out _);
                    return false;
                }

                Directory.Move(stagedMove.CurrentPath, stagedMove.NewPath);
                stagedMove.CurrentPath = stagedMove.NewPath;
            }

            return true;
        }
        catch (Exception ex)
        {
            TryRollbackStagedCaseFolderMutation(mutation, out _);
            errorMessage = $"No fue posible preparar la reorganizacion fisica del caso: {ex.Message}";
            mutation = null;
            return false;
        }
    }

    public bool TryRollbackStagedCaseFolderMutation(StagedCaseFolderMutation? mutation, out string? errorMessage)
    {
        errorMessage = null;
        if (mutation is null)
        {
            return true;
        }

        try
        {
            foreach (var stagedMove in mutation.ReorderedFolders.AsEnumerable().Reverse())
            {
                var sourcePath = Directory.Exists(stagedMove.CurrentPath)
                    ? stagedMove.CurrentPath
                    : Directory.Exists(stagedMove.TempPath)
                        ? stagedMove.TempPath
                        : null;

                if (string.IsNullOrWhiteSpace(sourcePath) || Directory.Exists(stagedMove.OriginalPath))
                {
                    continue;
                }

                Directory.Move(sourcePath, stagedMove.OriginalPath);
                stagedMove.CurrentPath = stagedMove.OriginalPath;
            }

            if (!string.IsNullOrWhiteSpace(mutation.StagedDeletedCasePath) &&
                !string.IsNullOrWhiteSpace(mutation.OriginalDeletedCasePath) &&
                Directory.Exists(mutation.StagedDeletedCasePath) &&
                !Directory.Exists(mutation.OriginalDeletedCasePath))
            {
                Directory.Move(mutation.StagedDeletedCasePath, mutation.OriginalDeletedCasePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"No fue posible revertir la reorganizacion fisica del caso: {ex.Message}";
            return false;
        }
    }

    public bool TryFinalizeStagedCaseFolderMutation(StagedCaseFolderMutation? mutation, out string? errorMessage)
    {
        errorMessage = null;
        if (mutation is null)
        {
            return true;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(mutation.StagedDeletedCasePath) &&
                Directory.Exists(mutation.StagedDeletedCasePath))
            {
                Directory.Delete(mutation.StagedDeletedCasePath, recursive: true);

                var oficioPath = Path.GetDirectoryName(mutation.StagedDeletedCasePath);
                if (!string.IsNullOrWhiteSpace(oficioPath))
                {
                    DeleteEmptyDirectoriesUpToRoot(oficioPath);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"El caso se elimino de la base, pero no fue posible completar la limpieza fisica: {ex.Message}";
            return false;
        }
    }

    public bool TryRenameOficioFolder(string oldNumeroOficio, string newNumeroOficio, out string? errorMessage)
    {
        errorMessage = null;
        var oldPath = GetOficioFolderPath(oldNumeroOficio);
        var newPath = GetOficioFolderPath(newNumeroOficio);

        if (oldPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Directory.Exists(oldPath))
        {
            return true;
        }

        try
        {
            if (Directory.Exists(newPath))
            {
                if (Directory.EnumerateFileSystemEntries(newPath).Any())
                {
                    errorMessage = "La carpeta de destino del oficio ya contiene archivos.";
                    return false;
                }

                Directory.Delete(newPath, recursive: true);
            }

            Directory.Move(oldPath, newPath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"No fue posible renombrar la carpeta fisica del oficio: {ex.Message}";
            return false;
        }
    }

    public bool TryReorderCaseFolders(
        string numeroOficio,
        IReadOnlyCollection<(int OldConsecutivo, int NewConsecutivo)> moves,
        out string? errorMessage)
    {
        errorMessage = null;
        if (moves.Count == 0)
        {
            return true;
        }

        var oficioPath = GetOficioFolderPath(numeroOficio);
        if (!Directory.Exists(oficioPath))
        {
            return true;
        }

        var tempMoves = new List<(string TempPath, string OldPath, string NewPath)>();

        try
        {
            foreach (var move in moves.Where(x => x.OldConsecutivo != x.NewConsecutivo).OrderBy(x => x.OldConsecutivo))
            {
                var oldPath = Path.Combine(oficioPath, move.OldConsecutivo.ToString("000"));
                if (!Directory.Exists(oldPath))
                {
                    continue;
                }

                var tempPath = Path.Combine(oficioPath, $"__tmp_{move.OldConsecutivo:000}_{Guid.NewGuid():N}");
                Directory.Move(oldPath, tempPath);
                tempMoves.Add((tempPath, oldPath, Path.Combine(oficioPath, move.NewConsecutivo.ToString("000"))));
            }

            foreach (var item in tempMoves)
            {
                if (Directory.Exists(item.NewPath))
                {
                    errorMessage = $"No se puede completar el reordenado; ya existe la carpeta destino {Path.GetFileName(item.NewPath)}.";
                    RollbackTempMoves(tempMoves);
                    return false;
                }

                Directory.Move(item.TempPath, item.NewPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"No fue posible reordenar carpetas de casos: {ex.Message}";
            RollbackTempMoves(tempMoves);
            return false;
        }
    }

    public string RebaseRelativePathForCase(
        string relativePath,
        string numeroOficio,
        int oldConsecutivo,
        int newConsecutivo)
    {
        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return relativePath;
        }

        var oficioFolders = GetOficioFolderCandidates(numeroOficio);
        if (!oficioFolders.Any(folder => parts[0].Equals(folder, StringComparison.OrdinalIgnoreCase)))
        {
            return relativePath;
        }

        if (!parts[1].Equals(oldConsecutivo.ToString("000"), StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        parts[1] = newConsecutivo.ToString("000");
        return string.Join('/', parts);
    }

    public string RebaseRelativePathForOficio(string relativePath, string oldNumeroOficio, string newNumeroOficio)
    {
        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
        {
            return relativePath;
        }

        var oldFolders = GetOficioFolderCandidates(oldNumeroOficio);
        var newFolder = GetOficioFolderName(newNumeroOficio);

        if (!oldFolders.Any(folder => parts[0].Equals(folder, StringComparison.OrdinalIgnoreCase)))
        {
            return relativePath;
        }

        parts[0] = newFolder;
        return string.Join('/', parts);
    }

    private static void RollbackTempMoves(IEnumerable<(string TempPath, string OldPath, string NewPath)> tempMoves)
    {
        foreach (var item in tempMoves)
        {
            try
            {
                if (Directory.Exists(item.TempPath) && !Directory.Exists(item.OldPath))
                {
                    Directory.Move(item.TempPath, item.OldPath);
                }
            }
            catch
            {
                // Best effort rollback.
            }
        }
    }

    private string? DeleteRelativeFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        if (!TryResolveSafeAbsolutePath(relativePath, out var absolutePath))
        {
            return null;
        }

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
            return absolutePath;
        }

        return null;
    }

    private void DeleteEmptyDirectoriesUpToRoot(string startDirectory)
    {
        var storageRoot = Path.GetFullPath(StorageRootPath);
        var storageRootPrefix = AppendDirectorySeparator(storageRoot);
        var current = Path.GetFullPath(startDirectory);

        while (current.StartsWith(storageRootPrefix, StringComparison.OrdinalIgnoreCase) &&
               !current.Equals(storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(current))
            {
                try
                {
                    if (Directory.EnumerateFileSystemEntries(current).Any())
                    {
                        break;
                    }

                    Directory.Delete(current, recursive: false);
                }
                catch
                {
                    break;
                }
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent))
            {
                break;
            }

            current = parent;
        }
    }

    private static string AppendDirectorySeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }

    private static bool TryReadImageDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!stream.CanRead)
        {
            return false;
        }

        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            return TryReadPngDimensions(reader, out width, out height) ||
                   TryReadGifDimensions(reader, out width, out height) ||
                   TryReadBmpDimensions(reader, out width, out height) ||
                   TryReadJpegDimensions(reader, out width, out height) ||
                   TryReadTiffDimensions(reader, out width, out height);
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryReadPngDimensions(BinaryReader reader, out int width, out int height)
    {
        width = 0;
        height = 0;
        reader.BaseStream.Position = 0;

        var header = reader.ReadBytes(24);
        if (header.Length < 24 ||
            header[0] != 0x89 ||
            header[1] != 0x50 ||
            header[2] != 0x4E ||
            header[3] != 0x47 ||
            header[4] != 0x0D ||
            header[5] != 0x0A ||
            header[6] != 0x1A ||
            header[7] != 0x0A ||
            header[12] != 0x49 ||
            header[13] != 0x48 ||
            header[14] != 0x44 ||
            header[15] != 0x52)
        {
            return false;
        }

        width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16, 4)));
        height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20, 4)));
        return width > 0 && height > 0;
    }

    private static bool TryReadGifDimensions(BinaryReader reader, out int width, out int height)
    {
        width = 0;
        height = 0;
        reader.BaseStream.Position = 0;

        var header = reader.ReadBytes(10);
        if (header.Length < 10 ||
            header[0] != 0x47 ||
            header[1] != 0x49 ||
            header[2] != 0x46 ||
            header[3] != 0x38 ||
            (header[4] != 0x37 && header[4] != 0x39) ||
            header[5] != 0x61)
        {
            return false;
        }

        width = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6, 2));
        height = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8, 2));
        return width > 0 && height > 0;
    }

    private static bool TryReadBmpDimensions(BinaryReader reader, out int width, out int height)
    {
        width = 0;
        height = 0;
        reader.BaseStream.Position = 0;

        var header = reader.ReadBytes(26);
        if (header.Length < 26 || header[0] != 0x42 || header[1] != 0x4D)
        {
            return false;
        }

        var dibHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(14, 4));
        if (dibHeaderSize == 12)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(18, 2));
            height = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(20, 2));
            return width > 0 && height > 0;
        }

        if (dibHeaderSize < 40)
        {
            return false;
        }

        width = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(18, 4)));
        height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(22, 4)));
        return width > 0 && height > 0;
    }

    private static bool TryReadJpegDimensions(BinaryReader reader, out int width, out int height)
    {
        width = 0;
        height = 0;
        reader.BaseStream.Position = 0;

        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
        {
            return false;
        }

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var prefix = reader.ReadByte();
            if (prefix != 0xFF)
            {
                continue;
            }

            byte marker;
            do
            {
                marker = reader.ReadByte();
            }
            while (marker == 0xFF);

            if (marker == 0xD9 || marker == 0xDA)
            {
                return false;
            }

            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                continue;
            }

            var segmentLength = ReadUInt16BigEndian(reader);
            if (segmentLength < 2)
            {
                return false;
            }

            if (IsJpegStartOfFrame(marker))
            {
                reader.ReadByte();
                height = ReadUInt16BigEndian(reader);
                width = ReadUInt16BigEndian(reader);
                return width > 0 && height > 0;
            }

            if (!SkipBytes(reader, segmentLength - 2))
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryReadTiffDimensions(BinaryReader reader, out int width, out int height)
    {
        width = 0;
        height = 0;
        reader.BaseStream.Position = 0;

        var header = reader.ReadBytes(8);
        if (header.Length < 8)
        {
            return false;
        }

        bool littleEndian;
        if (header[0] == 0x49 && header[1] == 0x49)
        {
            littleEndian = true;
        }
        else if (header[0] == 0x4D && header[1] == 0x4D)
        {
            littleEndian = false;
        }
        else
        {
            return false;
        }

        if (ReadUInt16(header.AsSpan(2, 2), littleEndian) != 42)
        {
            return false;
        }

        var ifdOffset = ReadUInt32(header.AsSpan(4, 4), littleEndian);
        if (ifdOffset > reader.BaseStream.Length - 2)
        {
            return false;
        }

        reader.BaseStream.Position = ifdOffset;
        var entryCount = ReadUInt16(reader, littleEndian);
        for (var index = 0; index < entryCount; index++)
        {
            var tag = ReadUInt16(reader, littleEndian);
            var type = ReadUInt16(reader, littleEndian);
            var count = ReadUInt32(reader, littleEndian);
            var valueBytes = reader.ReadBytes(4);
            if (valueBytes.Length < 4)
            {
                return false;
            }

            if (tag != 256 && tag != 257)
            {
                continue;
            }

            if (!TryReadTiffScalarValue(reader, littleEndian, type, count, valueBytes, out var value))
            {
                continue;
            }

            if (tag == 256)
            {
                width = value;
            }
            else
            {
                height = value;
            }
        }

        return width > 0 && height > 0;
    }

    private static bool TryReadTiffScalarValue(
        BinaryReader reader,
        bool littleEndian,
        ushort type,
        uint count,
        byte[] valueBytes,
        out int value)
    {
        value = 0;
        if (count == 0)
        {
            return false;
        }

        var bytesPerValue = type switch
        {
            3 => 2,
            4 => 4,
            _ => 0
        };

        if (bytesPerValue == 0)
        {
            return false;
        }

        if (count * bytesPerValue <= 4)
        {
            value = type switch
            {
                3 => ReadUInt16(valueBytes.AsSpan(0, 2), littleEndian),
                4 => checked((int)ReadUInt32(valueBytes.AsSpan(0, 4), littleEndian)),
                _ => 0
            };

            return value > 0;
        }

        var dataOffset = ReadUInt32(valueBytes.AsSpan(0, 4), littleEndian);
        if (dataOffset > reader.BaseStream.Length - bytesPerValue)
        {
            return false;
        }

        var returnPosition = reader.BaseStream.Position;
        reader.BaseStream.Position = dataOffset;
        try
        {
            value = type switch
            {
                3 => ReadUInt16(reader, littleEndian),
                4 => checked((int)ReadUInt32(reader, littleEndian)),
                _ => 0
            };
        }
        finally
        {
            reader.BaseStream.Position = returnPosition;
        }

        return value > 0;
    }

    private static bool IsJpegStartOfFrame(byte marker)
    {
        return marker switch
        {
            0xC0 or 0xC1 or 0xC2 or 0xC3 or
            0xC5 or 0xC6 or 0xC7 or
            0xC9 or 0xCA or 0xCB or
            0xCD or 0xCE or 0xCF => true,
            _ => false
        };
    }

    private static bool SkipBytes(BinaryReader reader, int count)
    {
        if (count < 0 || reader.BaseStream.Position > reader.BaseStream.Length - count)
        {
            return false;
        }

        reader.BaseStream.Position += count;
        return true;
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
    }

    private static ushort ReadUInt16(BinaryReader reader, bool littleEndian)
    {
        return ReadUInt16(reader.ReadBytes(2), littleEndian);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        return littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt16BigEndian(bytes);
    }

    private static uint ReadUInt32(BinaryReader reader, bool littleEndian)
    {
        return ReadUInt32(reader.ReadBytes(4), littleEndian);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, bool littleEndian)
    {
        return littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(bytes)
            : BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private bool TryResolveSafeAbsolutePath(string relativePath, out string absolutePath)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var storageRoot = Path.GetFullPath(StorageRootPath);
        var storageRootPrefix = AppendDirectorySeparator(storageRoot);
        var candidatePath = Path.GetFullPath(Path.Combine(storageRoot, normalized));

        if (!candidatePath.StartsWith(storageRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        absolutePath = candidatePath;
        return true;
    }

    private static string ResolveImageContentType(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    [SupportedOSPlatform("windows")]
    private static Bitmap CreateOptimizedImage(Image sourceImage, int maxDimension)
    {
        var safeMaxDimension = Math.Max(256, maxDimension);
        var normalized = DrawImageToBitmap(sourceImage, sourceImage.Width, sourceImage.Height);

        if (normalized.Width <= safeMaxDimension && normalized.Height <= safeMaxDimension)
        {
            return normalized;
        }

        var ratio = Math.Min(
            (double)safeMaxDimension / normalized.Width,
            (double)safeMaxDimension / normalized.Height);

        ratio = Math.Min(ratio, 1.0d);

        var width = Math.Max(1, (int)Math.Round(normalized.Width * ratio));
        var height = Math.Max(1, (int)Math.Round(normalized.Height * ratio));

        using (normalized)
        {
            return DrawImageToBitmap(normalized, width, height);
        }
    }

    [SupportedOSPlatform("windows")]
    private static Bitmap DrawImageToBitmap(Image sourceImage, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(sourceImage, 0, 0, width, height);

        return bitmap;
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyExifOrientation(Image image)
    {
        if (!image.PropertyIdList.Contains(ExifOrientationPropertyId))
        {
            return;
        }

        try
        {
            var property = image.GetPropertyItem(ExifOrientationPropertyId);
            var value = property?.Value;
            if (value is null || value.Length < 2)
            {
                return;
            }

            var orientationValue = BitConverter.ToUInt16(value, 0);
            var rotation = orientationValue switch
            {
                2 => RotateFlipType.RotateNoneFlipX,
                3 => RotateFlipType.Rotate180FlipNone,
                4 => RotateFlipType.Rotate180FlipX,
                5 => RotateFlipType.Rotate90FlipX,
                6 => RotateFlipType.Rotate90FlipNone,
                7 => RotateFlipType.Rotate270FlipX,
                8 => RotateFlipType.Rotate270FlipNone,
                _ => RotateFlipType.RotateNoneFlipNone
            };

            if (rotation != RotateFlipType.RotateNoneFlipNone)
            {
                image.RotateFlip(rotation);
            }

            try
            {
                image.RemovePropertyItem(ExifOrientationPropertyId);
            }
            catch
            {
                // Ignore when metadata cannot be removed.
            }
        }
        catch
        {
            // Ignore unreadable EXIF metadata.
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SaveJpegWithQuality(Image image, string destinationPath, int quality)
    {
        var safeQuality = Math.Clamp(quality, 40, 90);
        var jpegCodec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(x => x.FormatID == ImageFormat.Jpeg.Guid);

        if (jpegCodec is null)
        {
            image.Save(destinationPath, ImageFormat.Jpeg);
            return;
        }

        using var encoderParameters = new EncoderParameters(1);
        encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)safeQuality);
        image.Save(destinationPath, jpegCodec, encoderParameters);
    }

    private string GetOficioFolderPath(string numeroOficio)
    {
        return Path.Combine(StorageRootPath, GetOficioFolderName(numeroOficio));
    }

    private void StageEvidenceFile(string? relativePath, StagedEvidenceDeletion mutation)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        if (!TryResolveSafeAbsolutePath(relativePath, out var absolutePath))
        {
            throw new InvalidOperationException("La ruta de evidencia no es valida.");
        }

        if (!File.Exists(absolutePath))
        {
            return;
        }

        var stagedDirectory = Path.Combine(GetStagedDeleteRootPath(), "evidencias", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagedDirectory);

        var stagedPath = Path.Combine(stagedDirectory, Path.GetFileName(absolutePath));
        File.Move(absolutePath, stagedPath);

        mutation.Files.Add(new StagedEvidenceFile
        {
            OriginalPath = absolutePath,
            StagedPath = stagedPath
        });
    }

    private string GetStagedDeleteRootPath()
    {
        return Path.Combine(StorageRootPath, "__staged_deletes");
    }

    private void CleanupStagedDeleteDirectories(IEnumerable<string> stagedPaths)
    {
        foreach (var stagedPath in stagedPaths)
        {
            var stagedFolder = Path.GetDirectoryName(stagedPath);
            if (!string.IsNullOrWhiteSpace(stagedFolder))
            {
                DeleteEmptyDirectoriesUpToRoot(stagedFolder);
            }
        }
    }

    private string ResolveRootPath(IWebHostEnvironment environment)
    {
        if (Path.IsPathRooted(_options.RootPath))
        {
            return _options.RootPath;
        }

        return Path.Combine(environment.ContentRootPath, _options.RootPath);
    }

    private string StorageRootPath => ResolveRootPath(environment);

    private string GetOficioFolderName(string numeroOficio)
    {
        if (sensitiveDataProtection.IsConfigured)
        {
            var blindIndex = sensitiveDataProtection.BlindIndex(numeroOficio);
            if (!string.IsNullOrWhiteSpace(blindIndex))
            {
                return $"of_{blindIndex[..Math.Min(32, blindIndex.Length)]}";
            }
        }

        return OficioStoragePath.ToFolderName(numeroOficio);
    }

    private string[] GetOficioFolderCandidates(string numeroOficio)
    {
        var protectedName = GetOficioFolderName(numeroOficio);
        var legacyName = OficioStoragePath.ToFolderName(numeroOficio);
        return protectedName.Equals(legacyName, StringComparison.OrdinalIgnoreCase)
            ? [protectedName]
            : [protectedName, legacyName];
    }

    private string NormalizeRelative(string path) => path.Replace('\\', '/');

    private void CreateThumbnailSafe(string originalPath, string thumbnailPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.Copy(originalPath, thumbnailPath, overwrite: true);
            return;
        }

        CreateThumbnailWindows(originalPath, thumbnailPath);
    }

    [SupportedOSPlatform("windows")]
    private void CreateThumbnailWindows(string originalPath, string thumbnailPath)
    {
        try
        {
            using var sourceImage = Image.FromFile(originalPath);
            var ratio = Math.Min(
                (double)_options.ThumbnailWidth / sourceImage.Width,
                (double)_options.ThumbnailHeight / sourceImage.Height);

            ratio = Math.Min(ratio, 1.0d);

            var width = Math.Max(1, (int)Math.Round(sourceImage.Width * ratio));
            var height = Math.Max(1, (int)Math.Round(sourceImage.Height * ratio));

            using var thumbnail = DrawImageToBitmap(sourceImage, width, height);
            SaveJpegWithQuality(thumbnail, thumbnailPath, _options.JpegQuality);
        }
        catch
        {
            File.Copy(originalPath, thumbnailPath, overwrite: true);
        }
    }

    private void ProtectEvidenceFileInPlace(string absolutePath)
    {
        if (!sensitiveDataProtection.IsConfigured)
        {
            return;
        }

        var bytes = File.ReadAllBytes(absolutePath);
        if (sensitiveDataProtection.IsProtectedPayload(bytes))
        {
            return;
        }

        var protectedBytes = sensitiveDataProtection.ProtectBytes(bytes);
        File.WriteAllBytes(absolutePath, protectedBytes);
    }
}
