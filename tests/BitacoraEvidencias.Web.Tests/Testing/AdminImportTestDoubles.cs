using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Http;

namespace BitacoraEvidencias.Web.Tests.Testing;

public sealed class RecordingAuditLogService : IAuditLogService
{
    public List<AuditLogEntry> Entries { get; } = [];

    public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

public sealed class FakeTabularImportFileParser : ITabularImportFileParser
{
    public TabularImportFile Result { get; set; } = new();

    public Task<TabularImportFile> ParseAsync(IFormFile file, CancellationToken cancellationToken = default)
        => Task.FromResult(Result);
}

public sealed class MemoryImportPreviewStore<TPreview> : IImportPreviewStore
    where TPreview : class
{
    public TPreview? StoredPreview { get; set; }
    public int SaveCount { get; private set; }

    public Task SaveAsync<T>(string scope, string ownerKey, T preview, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        StoredPreview = preview as TPreview;
        return Task.CompletedTask;
    }

    public Task<T?> LoadAsync<T>(string scope, string ownerKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((T?)(object?)StoredPreview);
    }

    public Task DeleteAsync(string scope, string ownerKey, CancellationToken cancellationToken = default)
    {
        StoredPreview = null;
        return Task.CompletedTask;
    }
}
