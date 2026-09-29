using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface IEvidenciaImportPreviewService
{
    Task<ImportEvidenciasModel.EvidenciaImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<ImportEvidenciasModel.EvidenciaImportPreview> BuildPreviewFromRowsAsync(string fileName, IReadOnlyCollection<ImportEvidenciasModel.EvidenciaImportRow> rows, CancellationToken cancellationToken = default);
}

public sealed class EvidenciaImportPreviewService(
    AppDbContext dbContext,
    ITabularImportFileParser tabularImportFileParser,
    IPhotoStorageService photoStorageService) : IEvidenciaImportPreviewService
{
    private static readonly string[] ExpectedHeaders =
    [
        "OFICIO",
        "ARCHIVO",
        "TIPO",
        "NIVEL",
        "FECHA",
        "NOTAS"
    ];

    private static readonly StringComparer FilePathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public async Task<ImportEvidenciasModel.EvidenciaImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var preview = new ImportEvidenciasModel.EvidenciaImportPreview
        {
            FileName = file.FileName
        };

        var tabularFile = await tabularImportFileParser.ParseAsync(file, cancellationToken);
        foreach (var parserError in tabularFile.Errors)
        {
            preview.Errors.Add(new ImportEvidenciasModel.ImportError
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
            preview.Errors.Add(new ImportEvidenciasModel.ImportError
            {
                Message = "El archivo no contiene registros para importar."
            });
            return preview;
        }

        var candidates = tabularFile.Rows
            .Select(row => new EvidenciaImportCandidate
            {
                SourceRowNumber = row.SourceRowNumber,
                NumeroOficio = GetColumnValue(row, 0),
                SourceFilePath = GetColumnValue(row, 1),
                TipoEvidencia = GetColumnValue(row, 2),
                NivelEducativo = GetColumnValue(row, 3),
                FechaRaw = GetColumnValue(row, 4),
                Notas = GetColumnValue(row, 5)
            })
            .ToList();

        await PopulatePreviewAsync(preview, candidates, cancellationToken);
        return preview;
    }

    public async Task<ImportEvidenciasModel.EvidenciaImportPreview> BuildPreviewFromRowsAsync(
        string fileName,
        IReadOnlyCollection<ImportEvidenciasModel.EvidenciaImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var preview = new ImportEvidenciasModel.EvidenciaImportPreview
        {
            FileName = fileName,
            TotalRows = rows.Count
        };

        var candidates = rows
            .OrderBy(row => row.SourceRowNumber)
            .Select(row => new EvidenciaImportCandidate
            {
                SourceRowNumber = row.SourceRowNumber,
                NumeroOficio = row.NumeroOficio,
                SourceFilePath = row.SourceFilePath,
                TipoEvidencia = row.TipoEvidencia,
                NivelEducativo = row.NivelEducativo,
                FechaRaw = row.FechaEvidencia.ToString("dd/MM/yyyy"),
                Notas = row.Notas
            })
            .ToList();

        await PopulatePreviewAsync(preview, candidates, cancellationToken);
        return preview;
    }

    private async Task PopulatePreviewAsync(
        ImportEvidenciasModel.EvidenciaImportPreview preview,
        IReadOnlyList<EvidenciaImportCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            preview.Errors.Add(new ImportEvidenciasModel.ImportError
            {
                Message = "El archivo no contiene registros para importar."
            });
            return;
        }

        var caseTargets = await LoadCaseTargetsAsync(cancellationToken);
        var pathRows = new Dictionary<string, List<int>>(FilePathComparer);

        foreach (var candidate in candidates)
        {
            if (!TryNormalizeLocalAbsolutePath(candidate.SourceFilePath, out var normalizedPath, out _))
            {
                continue;
            }

            if (!pathRows.TryGetValue(normalizedPath!, out var rows))
            {
                rows = [];
                pathRows[normalizedPath!] = rows;
            }

            rows.Add(candidate.SourceRowNumber);
        }

        foreach (var candidate in candidates)
        {
            var rowErrors = new List<string>();
            var numeroOficio = candidate.NumeroOficio?.Trim() ?? string.Empty;
            var tipoEvidencia = NormalizeCatalogValueStrict(candidate.TipoEvidencia, CatalogosCaso.TiposEvidencia);
            var nivelEducativo = NormalizeCatalogValueStrict(candidate.NivelEducativo, CatalogosCaso.NivelesEducativos);
            var notas = ImportParsing.NormalizeOptional(candidate.Notas);

            if (string.IsNullOrWhiteSpace(numeroOficio))
            {
                rowErrors.Add("OFICIO es obligatorio.");
            }
            else if (numeroOficio.Length > 80)
            {
                rowErrors.Add("OFICIO excede 80 caracteres.");
            }

            if (tipoEvidencia is null)
            {
                rowErrors.Add("TIPO debe ser Oficio, Lista o Extra.");
            }

            if (nivelEducativo is null)
            {
                rowErrors.Add("NIVEL debe ser Primaria o Secundaria.");
            }

            if (!ImportParsing.TryParseRequiredDate(candidate.FechaRaw, out var fechaEvidencia))
            {
                rowErrors.Add("FECHA no tiene un formato valido. Usa dd/MM/yyyy.");
            }

            string? normalizedPath = null;
            if (!TryNormalizeLocalAbsolutePath(candidate.SourceFilePath, out normalizedPath, out var pathError))
            {
                rowErrors.Add(pathError!);
            }
            else
            {
                var duplicateRows = pathRows[normalizedPath!]
                    .Where(rowNumber => rowNumber != candidate.SourceRowNumber)
                    .OrderBy(rowNumber => rowNumber)
                    .ToList();
                if (duplicateRows.Count > 0)
                {
                    rowErrors.Add($"ARCHIVO repetido dentro del archivo. Tambien aparece en la(s) fila(s): {string.Join(", ", duplicateRows)}.");
                }

                if (!photoStorageService.TryValidateSourceEvidenceFile(normalizedPath!, out var fileError))
                {
                    rowErrors.Add(fileError!);
                }
            }

            CaseImportTarget? caseTarget = null;
            if (!string.IsNullOrWhiteSpace(numeroOficio) &&
                numeroOficio.Length <= 80 &&
                nivelEducativo is not null &&
                !TryResolveCaseTarget(caseTargets, numeroOficio, nivelEducativo, out caseTarget, out var caseError))
            {
                rowErrors.Add(caseError!);
            }

            if (rowErrors.Count > 0)
            {
                foreach (var error in rowErrors)
                {
                    preview.Errors.Add(new ImportEvidenciasModel.ImportError
                    {
                        RowNumber = candidate.SourceRowNumber,
                        Message = error
                    });
                }

                continue;
            }

            preview.Rows.Add(new ImportEvidenciasModel.EvidenciaImportRow
            {
                SourceRowNumber = candidate.SourceRowNumber,
                NumeroOficio = numeroOficio,
                SourceFilePath = normalizedPath!,
                TipoEvidencia = tipoEvidencia!,
                NivelEducativo = nivelEducativo!,
                FechaEvidencia = fechaEvidencia.Date,
                Notas = notas,
                TargetConsecutivo = caseTarget!.Consecutivo
            });
        }

        preview.InvalidRows = preview.TotalRows - preview.Rows.Count;
    }

    private async Task<Dictionary<string, List<CaseImportTarget>>> LoadCaseTargetsAsync(CancellationToken cancellationToken)
    {
        var cases = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.Oficio != null)
            .Select(x => new CaseImportTarget
            {
                OficioId = x.OficioId,
                CasoCorreccionId = x.Id,
                NumeroOficio = x.Oficio!.NumeroOficio,
                Consecutivo = x.Consecutivo,
                NivelEducativo = x.NivelEducativo,
                AuditoriaEstado = x.AuditoriaEstado
            })
            .ToListAsync(cancellationToken);

        return cases
            .GroupBy(item => NormalizeLookupKey(item.NumeroOficio), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Consecutivo).ToList(),
                StringComparer.Ordinal);
    }

    private static void ValidateExpectedHeaders(IReadOnlyList<string> headers, ICollection<ImportEvidenciasModel.ImportError> errors)
    {
        ImportPreviewSupport.ValidateExactHeaders(
            headers,
            ExpectedHeaders,
            errors,
            message => new ImportEvidenciasModel.ImportError { Message = message });
    }

    private static string? GetColumnValue(TabularImportRow row, int index)
        => ImportPreviewSupport.GetColumnValue(row, index);

    private static string? NormalizeCatalogValueStrict(string? rawValue, IReadOnlyList<string> allowedValues)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        return allowedValues.FirstOrDefault(value => string.Equals(value, trimmed, StringComparison.Ordinal));
    }

    private static bool TryNormalizeLocalAbsolutePath(string? rawPath, out string? normalizedPath, out string? errorMessage)
    {
        normalizedPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rawPath))
        {
            errorMessage = "ARCHIVO es obligatorio.";
            return false;
        }

        var trimmedPath = rawPath.Trim();
        if (!Path.IsPathFullyQualified(trimmedPath))
        {
            errorMessage = "ARCHIVO debe ser una ruta absoluta local del servidor.";
            return false;
        }

        if (trimmedPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            errorMessage = "ARCHIVO debe ser una ruta local del servidor. No se permiten rutas de red.";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(trimmedPath);
            return true;
        }
        catch
        {
            errorMessage = "ARCHIVO no contiene una ruta valida.";
            return false;
        }
    }

    private static bool TryResolveCaseTarget(
        IReadOnlyDictionary<string, List<CaseImportTarget>> caseTargets,
        string numeroOficio,
        string nivelEducativo,
        out CaseImportTarget? caseTarget,
        out string? errorMessage)
    {
        var normalizedNumeroOficio = NormalizeLookupKey(numeroOficio);
        if (string.IsNullOrWhiteSpace(normalizedNumeroOficio) ||
            !caseTargets.TryGetValue(normalizedNumeroOficio, out var officeCases))
        {
            caseTarget = null;
            errorMessage = "OFICIO no existe en la base.";
            return false;
        }

        var exactOfficeCases = officeCases
            .Where(item => string.Equals(item.NumeroOficio.Trim(), numeroOficio.Trim(), StringComparison.Ordinal))
            .ToList();

        var candidateOfficeCases = exactOfficeCases.Count > 0
            ? exactOfficeCases
            : officeCases;

        if (exactOfficeCases.Count == 0 &&
            candidateOfficeCases
                .Select(item => item.OficioId)
                .Distinct()
                .Skip(1)
                .Any())
        {
            caseTarget = null;
            errorMessage = "OFICIO coincide con mas de un registro existente. Revisa el formato del numero de oficio.";
            return false;
        }

        var normalizedNivel = NormalizeLookupKey(nivelEducativo);
        var matches = candidateOfficeCases
            .Where(item => string.Equals(NormalizeLookupKey(item.NivelEducativo), normalizedNivel, StringComparison.Ordinal))
            .OrderBy(item => item.Consecutivo)
            .ToList();

        if (matches.Count == 0)
        {
            caseTarget = null;
            errorMessage = $"OFICIO no tiene un caso para NIVEL '{nivelEducativo}'.";
            return false;
        }

        if (matches.Count > 1)
        {
            caseTarget = null;
            errorMessage = $"OFICIO tiene mas de un caso para NIVEL '{nivelEducativo}'.";
            return false;
        }

        caseTarget = matches[0];
        errorMessage = null;
        return true;
    }

    private static string NormalizeLookupKey(string? value)
        => ImportParsing.NormalizeHeader(value);

    private sealed class EvidenciaImportCandidate
    {
        public int SourceRowNumber { get; init; }
        public string? NumeroOficio { get; init; }
        public string? SourceFilePath { get; init; }
        public string? TipoEvidencia { get; init; }
        public string? NivelEducativo { get; init; }
        public string? FechaRaw { get; init; }
        public string? Notas { get; init; }
    }

    private sealed class CaseImportTarget
    {
        public int OficioId { get; init; }
        public int CasoCorreccionId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string AuditoriaEstado { get; set; } = CatalogosAuditoriaCaso.Pendiente;
    }
}
