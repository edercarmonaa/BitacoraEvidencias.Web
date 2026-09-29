using Microsoft.AspNetCore.Http;

namespace BitacoraEvidencias.Web.Services;

public static class ImportPreviewSupport
{
    public static void ValidateExactHeaders<TError>(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> expectedHeaders,
        ICollection<TError> errors,
        Func<string, TError> errorFactory)
    {
        var normalizedHeaders = headers
            .Select(header => header.Trim())
            .ToList();

        while (normalizedHeaders.Count > 0 && string.IsNullOrWhiteSpace(normalizedHeaders[^1]))
        {
            normalizedHeaders.RemoveAt(normalizedHeaders.Count - 1);
        }

        if (normalizedHeaders.Count != expectedHeaders.Count)
        {
            errors.Add(errorFactory(BuildExactHeadersMessage(expectedHeaders)));
            return;
        }

        for (var index = 0; index < expectedHeaders.Count; index++)
        {
            if (!string.Equals(normalizedHeaders[index], expectedHeaders[index], StringComparison.Ordinal))
            {
                errors.Add(errorFactory(BuildExactHeadersMessage(expectedHeaders)));
                return;
            }
        }
    }

    public static string? GetColumnValue(TabularImportRow row, int index)
        => index < row.Values.Count ? row.Values[index] : null;

    private static string BuildExactHeadersMessage(IReadOnlyList<string> expectedHeaders)
        => $"Los encabezados deben ser exactamente y en este orden: {string.Join(", ", expectedHeaders)}.";
}
