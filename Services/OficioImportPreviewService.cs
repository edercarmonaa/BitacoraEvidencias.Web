using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface IOficioImportPreviewService
{
    Task<ImportOficiosModel.OficioImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<ImportOficiosModel.OficioImportPreview> BuildPreviewFromRowsAsync(string fileName, IReadOnlyCollection<ImportOficiosModel.OficioImportRow> rows, CancellationToken cancellationToken = default);
}

public sealed class OficioImportPreviewService(
    AppDbContext dbContext,
    ITabularImportFileParser tabularImportFileParser) : IOficioImportPreviewService
{
    private static readonly string[] ExpectedHeaders =
    [
        "NumeroOficio",
        "FechaOficio",
        "Asunto",
        "Notas"
    ];

    public async Task<ImportOficiosModel.OficioImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var preview = new ImportOficiosModel.OficioImportPreview
        {
            FileName = file.FileName
        };

        var tabularFile = await tabularImportFileParser.ParseAsync(file, cancellationToken);
        foreach (var parserError in tabularFile.Errors)
        {
            preview.Errors.Add(new ImportOficiosModel.ImportError
            {
                Message = parserError
            });
        }

        preview.TotalRows = tabularFile.TotalRows;
        if (preview.Errors.Count > 0)
        {
            preview.InvalidRows = preview.TotalRows;
            return preview;
        }

        ValidateExpectedHeaders(tabularFile.Headers, preview.Errors);
        if (preview.Errors.Count > 0)
        {
            preview.InvalidRows = preview.TotalRows;
            return preview;
        }

        if (tabularFile.Rows.Count == 0)
        {
            preview.Errors.Add(new ImportOficiosModel.ImportError
            {
                Message = "El archivo no contiene registros para importar."
            });
            return preview;
        }

        var candidates = tabularFile.Rows
            .Select(row => new OficioImportCandidate
            {
                SourceRowNumber = row.SourceRowNumber,
                NumeroOficio = GetColumnValue(row, 0),
                FechaRaw = GetColumnValue(row, 1),
                Asunto = GetColumnValue(row, 2),
                Notas = GetColumnValue(row, 3)
            })
            .ToList();

        await PopulatePreviewAsync(preview, candidates, cancellationToken);
        return preview;
    }

    public async Task<ImportOficiosModel.OficioImportPreview> BuildPreviewFromRowsAsync(
        string fileName,
        IReadOnlyCollection<ImportOficiosModel.OficioImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var preview = new ImportOficiosModel.OficioImportPreview
        {
            FileName = fileName,
            TotalRows = rows.Count
        };

        var candidates = rows
            .OrderBy(row => row.SourceRowNumber)
            .Select(row => new OficioImportCandidate
            {
                SourceRowNumber = row.SourceRowNumber,
                NumeroOficio = row.NumeroOficio,
                FechaRaw = row.FechaOficio.ToString("dd/MM/yyyy"),
                Asunto = row.Asunto,
                Notas = row.Notas
            })
            .ToList();

        await PopulatePreviewAsync(preview, candidates, cancellationToken);
        return preview;
    }

    private async Task PopulatePreviewAsync(
        ImportOficiosModel.OficioImportPreview preview,
        IReadOnlyList<OficioImportCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            preview.Errors.Add(new ImportOficiosModel.ImportError
            {
                Message = "El archivo no contiene registros para importar."
            });
            return;
        }

        var existingNumbers = await dbContext.Oficios
            .AsNoTracking()
            .Select(x => x.NumeroOficio)
            .ToListAsync(cancellationToken);

        var seenNumbers = new HashSet<string>(StringComparer.Ordinal);
        var seenFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var rowErrors = new List<string>();
            var numeroOficio = candidate.NumeroOficio?.Trim() ?? string.Empty;
            var asunto = candidate.Asunto?.Trim() ?? string.Empty;
            var notas = ImportParsing.NormalizeOptional(candidate.Notas);

            if (string.IsNullOrWhiteSpace(numeroOficio))
            {
                rowErrors.Add("NumeroOficio es obligatorio.");
            }
            else
            {
                if (numeroOficio.Length > 80)
                {
                    rowErrors.Add("NumeroOficio excede 80 caracteres.");
                }

                if (!seenNumbers.Add(numeroOficio))
                {
                    rowErrors.Add("NumeroOficio repetido dentro del archivo.");
                }

                if (existingNumbers.Contains(numeroOficio, StringComparer.Ordinal))
                {
                    rowErrors.Add("NumeroOficio ya existe en la base.");
                }
                else if (OficioStoragePath.TryFindFolderCollision(numeroOficio, existingNumbers, out var conflictingNumero))
                {
                    rowErrors.Add($"NumeroOficio colisiona con la carpeta de '{conflictingNumero}'.");
                }

                var folderName = OficioStoragePath.ToFolderName(numeroOficio);
                if (seenFolders.TryGetValue(folderName, out var conflictingInFile) &&
                    !string.Equals(conflictingInFile, numeroOficio, StringComparison.Ordinal))
                {
                    rowErrors.Add($"NumeroOficio colisiona en carpeta con '{conflictingInFile}' dentro del archivo.");
                }
                else
                {
                    seenFolders.TryAdd(folderName, numeroOficio);
                }
            }

            if (!ImportParsing.TryParseRequiredDate(candidate.FechaRaw, out var fechaOficio))
            {
                rowErrors.Add("FechaOficio no tiene un formato valido. Usa dd/MM/yyyy.");
            }

            if (string.IsNullOrWhiteSpace(asunto))
            {
                rowErrors.Add("Asunto es obligatorio.");
            }
            else if (asunto.Length > 200)
            {
                rowErrors.Add("Asunto excede 200 caracteres.");
            }

            if (notas is not null && notas.Length > 255)
            {
                rowErrors.Add("Notas excede 255 caracteres.");
            }

            if (rowErrors.Count > 0)
            {
                foreach (var error in rowErrors)
                {
                    preview.Errors.Add(new ImportOficiosModel.ImportError
                    {
                        RowNumber = candidate.SourceRowNumber,
                        Message = error
                    });
                }

                continue;
            }

            preview.Rows.Add(new ImportOficiosModel.OficioImportRow
            {
                SourceRowNumber = candidate.SourceRowNumber,
                NumeroOficio = numeroOficio,
                FechaOficio = fechaOficio,
                Asunto = asunto,
                Notas = notas
            });
        }

        preview.InvalidRows = preview.TotalRows - preview.Rows.Count;
    }

    private static void ValidateExpectedHeaders(IReadOnlyList<string> headers, ICollection<ImportOficiosModel.ImportError> errors)
    {
        ImportPreviewSupport.ValidateExactHeaders(
            headers,
            ExpectedHeaders,
            errors,
            message => new ImportOficiosModel.ImportError { Message = message });
    }

    private static string? GetColumnValue(TabularImportRow row, int index)
        => ImportPreviewSupport.GetColumnValue(row, index);

    private sealed class OficioImportCandidate
    {
        public int SourceRowNumber { get; init; }
        public string? NumeroOficio { get; init; }
        public string? FechaRaw { get; init; }
        public string? Asunto { get; init; }
        public string? Notas { get; init; }
    }
}
