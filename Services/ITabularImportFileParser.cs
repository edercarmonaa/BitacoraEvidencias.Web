using Microsoft.AspNetCore.Http;

namespace BitacoraEvidencias.Web.Services;

public interface ITabularImportFileParser
{
    Task<TabularImportFile> ParseAsync(IFormFile file, CancellationToken cancellationToken = default);
}
