using System.Security.Cryptography;
using System.Text.Json;

namespace BitacoraEvidencias.Web.Services;

public class ImportPreviewStore : IImportPreviewStore
{
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string storageRoot;

    public ImportPreviewStore()
    {
        storageRoot = Path.Combine(Path.GetTempPath(), "BitacoraEvidencias.Web", "ImportPreviews");
    }

    public async Task SaveAsync<T>(string scope, string ownerKey, T preview, CancellationToken cancellationToken = default)
    {
        CleanupExpiredFiles();
        var path = GetPreviewPath(scope, ownerKey);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, preview, SerializerOptions, cancellationToken);
    }

    public async Task<T?> LoadAsync<T>(string scope, string ownerKey, CancellationToken cancellationToken = default)
    {
        CleanupExpiredFiles();
        var path = GetPreviewPath(scope, ownerKey);
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken);
        }
        catch (IOException)
        {
            return default;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public Task DeleteAsync(string scope, string ownerKey, CancellationToken cancellationToken = default)
    {
        CleanupExpiredFiles();
        var path = GetPreviewPath(scope, ownerKey);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Ignore cleanup failures; preview files are disposable.
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore cleanup failures; preview files are disposable.
        }

        return Task.CompletedTask;
    }

    private string GetPreviewPath(string scope, string ownerKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerKey);

        var scopeFolder = Path.Combine(storageRoot, SanitizeScope(scope));
        var ownerHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ownerKey)))
            .ToLowerInvariant();
        return Path.Combine(scopeFolder, $"{ownerHash}.json");
    }

    private static string SanitizeScope(string scope)
    {
        var sanitized = new string(scope
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }

    private void CleanupExpiredFiles()
    {
        if (!Directory.Exists(storageRoot))
        {
            return;
        }

        var thresholdUtc = DateTime.UtcNow - PreviewLifetime;
        foreach (var path in Directory.EnumerateFiles(storageRoot, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var lastWriteUtc = File.GetLastWriteTimeUtc(path);
                if (lastWriteUtc < thresholdUtc)
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore cleanup failures; preview files are disposable.
            }
        }
    }
}
