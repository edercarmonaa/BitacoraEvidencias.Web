namespace BitacoraEvidencias.Web.Services;

public interface IImportPreviewStore
{
    Task SaveAsync<T>(string scope, string ownerKey, T preview, CancellationToken cancellationToken = default);
    Task<T?> LoadAsync<T>(string scope, string ownerKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string scope, string ownerKey, CancellationToken cancellationToken = default);
}
