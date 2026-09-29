using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Admin;

[Authorize(Roles = SecurityDefaults.AdminRole)]
public class ImportEvidenciasModel(
    AppDbContext dbContext,
    IImportPreviewStore importPreviewStore,
    IPhotoStorageService photoStorageService,
    IEvidenciaImportApplicationService evidenciaImportApplicationService,
    IEvidenciaImportPreviewService evidenciaImportPreviewService) : PageModel


{
    private const int DefaultPreviewPageSize = 20;
    private const int MaxPreviewPageSize = 100;
    private const string PreviewScope = "evidencias";

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

    [BindProperty]
    public UploadInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPreviewPageSize;

    public EvidenciaImportPreview? Preview { get; private set; }
    public IReadOnlyList<EvidenciaImportRow> PreviewRowsPage { get; private set; } = [];
    public int PreviewRowsTotalPages { get; private set; } = 1;
    public int MaxAllowedPageSize => MaxPreviewPageSize;

    [TempData]
    public string? FlashSuccess { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPreviewAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        var previewOwnerKey = GetPreviewOwnerKey();
        await importPreviewStore.DeleteAsync(PreviewScope, previewOwnerKey, cancellationToken);
        PageNumber = 1;
        PageSize = ImportPreviewPagingSupport.NormalizePageSize(PageSize, DefaultPreviewPageSize, MaxPreviewPageSize);


        if (Input.File is null || Input.File.Length == 0)
        {
            ModelState.AddModelError(nameof(Input.File), "Selecciona un archivo .csv o .xlsx.");
            return Page();
        }

        Preview = await BuildPreviewAsync(Input.File, cancellationToken);
        if (Preview.CanImport)
        {
            await importPreviewStore.SaveAsync(PreviewScope, previewOwnerKey, Preview, cancellationToken);
        }

        ApplyPreviewPaging(Preview);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(CancellationToken cancellationToken)
    {
        await importPreviewStore.DeleteAsync(PreviewScope, GetPreviewOwnerKey(), cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostImportAsync(CancellationToken cancellationToken)
    {
        var previewOwnerKey = GetPreviewOwnerKey();
        var preview = await importPreviewStore.LoadAsync<EvidenciaImportPreview>(PreviewScope, previewOwnerKey, cancellationToken);
        if (preview is null)
        {
            FlashError = "La vista previa expiro o ya no existe. Carga el archivo nuevamente.";
            return RedirectToPage();
        }

        Preview = preview;
        if (!preview.CanImport)
        {
            await importPreviewStore.DeleteAsync(PreviewScope, previewOwnerKey, cancellationToken);
            FlashError = "La vista previa no contiene registros validos para importar.";
            return RedirectToPage();
        }

        var result = await evidenciaImportApplicationService.ImportAsync(preview, cancellationToken);
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            await importPreviewStore.DeleteAsync(PreviewScope, previewOwnerKey, cancellationToken);
            FlashError = result.ErrorMessage;
            return RedirectToPage();
        }

        if (result.FailedRows.Count > 0)
        {
            var retryPreview = await BuildPreviewFromRowsAsync(preview.FileName, result.FailedRows, cancellationToken);
            await importPreviewStore.SaveAsync(PreviewScope, previewOwnerKey, retryPreview, cancellationToken);

            if (result.ImportedCount > 0)
            {
                FlashSuccess = $"Se importaron {result.ImportedCount} evidencia(s) correctamente desde {preview.FileName}.";
            }

            FlashError = $"Quedaron {result.FailedRows.Count} fila(s) pendientes. Se genero una nueva vista previa con los registros no importados.";
            return RedirectToPage();
        }

        await importPreviewStore.DeleteAsync(PreviewScope, previewOwnerKey, cancellationToken);
        FlashSuccess = $"Se importaron {result.ImportedCount} evidencia(s) correctamente desde {preview.FileName}.";
        return RedirectToPage();
    }
    private Task<EvidenciaImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken)
{
    return evidenciaImportPreviewService.BuildPreviewAsync(file, cancellationToken);
}
private Task<EvidenciaImportPreview> BuildPreviewFromRowsAsync(
    string fileName,
    IReadOnlyCollection<EvidenciaImportRow> rows,
    CancellationToken cancellationToken)
{
    return evidenciaImportPreviewService.BuildPreviewFromRowsAsync(fileName, rows, cancellationToken);
}

    private async Task PopulatePreviewAsync(
        EvidenciaImportPreview preview,
        IReadOnlyList<EvidenciaImportCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            preview.Errors.Add(new ImportError
            {
                Message = "El archivo no contiene registros para importar."
            });
            return;
        }

        var caseTargets = await LoadCaseTargetsAsync(cancellationToken);
        var normalizedPathsByRow = new Dictionary<int, string>();
        var pathRows = new Dictionary<string, List<int>>(FilePathComparer);

        foreach (var candidate in candidates)
        {
            if (!TryNormalizeLocalAbsolutePath(candidate.SourceFilePath, out var normalizedPath, out _))
            {
                continue;
            }

            normalizedPathsByRow[candidate.SourceRowNumber] = normalizedPath!;
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
                    preview.Errors.Add(new ImportError
                    {
                        RowNumber = candidate.SourceRowNumber,
                        Message = error
                    });
                }

                continue;
            }

            preview.Rows.Add(new EvidenciaImportRow
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
            .GroupBy(item => NormalizeOfficeLookupKey(item.NumeroOficio), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Consecutivo).ToList(),
                StringComparer.Ordinal);
    }

    private async Task LoadPreviewAsync(CancellationToken cancellationToken)
    {
        Preview = await importPreviewStore.LoadAsync<EvidenciaImportPreview>(
            PreviewScope,
            GetPreviewOwnerKey(),
            cancellationToken);
        if (Preview is null)
        {
            return;
        }

        ApplyPreviewPaging(Preview);
    }

    private string GetPreviewOwnerKey()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.Identity?.Name?.Trim()
            ?? throw new InvalidOperationException("No fue posible identificar al usuario actual.");
    }
private void ApplyPreviewPaging(EvidenciaImportPreview preview)
{
    PageSize = ImportPreviewPagingSupport.NormalizePageSize(PageSize, DefaultPreviewPageSize, MaxPreviewPageSize);

    var paging = ImportPreviewPagingSupport.ApplyPaging(preview.Rows, PageNumber, PageSize, row => row.SourceRowNumber);
    PageNumber = paging.PageNumber;
    PreviewRowsTotalPages = paging.TotalPages;
    PreviewRowsPage = paging.Rows;
}

    
    private static void ValidateExpectedHeaders(IReadOnlyList<string> headers, ICollection<ImportError> errors)
    {
        var normalizedHeaders = headers
            .Select(header => header.Trim())
            .ToList();

        while (normalizedHeaders.Count > 0 &&
               string.IsNullOrWhiteSpace(normalizedHeaders[^1]))
        {
            normalizedHeaders.RemoveAt(normalizedHeaders.Count - 1);
        }

        if (normalizedHeaders.Count != ExpectedHeaders.Length)
        {
            errors.Add(new ImportError
            {
                Message = "Los encabezados deben ser exactamente y en este orden: OFICIO, ARCHIVO, TIPO, NIVEL, FECHA, NOTAS."
            });
            return;
        }

        for (var index = 0; index < ExpectedHeaders.Length; index++)
        {
            if (!string.Equals(normalizedHeaders[index], ExpectedHeaders[index], StringComparison.Ordinal))
            {
                errors.Add(new ImportError
                {
                    Message = "Los encabezados deben ser exactamente y en este orden: OFICIO, ARCHIVO, TIPO, NIVEL, FECHA, NOTAS."
                });
                return;
            }
        }
    }

    private static string? GetColumnValue(TabularImportRow row, int index)
    {
        return index < row.Values.Count ? row.Values[index] : null;
    }

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
        var normalizedNumeroOficio = NormalizeOfficeLookupKey(numeroOficio);
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

        var normalizedNivel = NormalizeCatalogLookupKey(nivelEducativo);
        var matches = candidateOfficeCases
            .Where(item => string.Equals(NormalizeCatalogLookupKey(item.NivelEducativo), normalizedNivel, StringComparison.Ordinal))
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

    private static string NormalizeOfficeLookupKey(string? value)
    {
        return ImportParsing.NormalizeHeader(value);
    }

    private static string NormalizeCatalogLookupKey(string? value)
    {
        return ImportParsing.NormalizeHeader(value);
    }

    public class UploadInput
    {
        [Display(Name = "Archivo")]
        public IFormFile? File { get; set; }
    }

    public class EvidenciaImportPreview
    {
        public string FileName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int InvalidRows { get; set; }
        public List<EvidenciaImportRow> Rows { get; set; } = [];
        public List<ImportError> Errors { get; set; } = [];
        public int ValidRows => Rows.Count;
        public bool HasBlockingErrors => Errors.Any(error => error.RowNumber <= 0);
        public bool CanImport => ValidRows > 0 && !HasBlockingErrors;
    }

    public class EvidenciaImportRow
    {
        public int SourceRowNumber { get; set; }
        public string NumeroOficio { get; set; } = string.Empty;
        public string SourceFilePath { get; set; } = string.Empty;
        public string TipoEvidencia { get; set; } = string.Empty;
        public string NivelEducativo { get; set; } = string.Empty;
        public DateTime FechaEvidencia { get; set; }
        public string? Notas { get; set; }
        public int TargetConsecutivo { get; set; }
        public string TargetConsecutivoTexto => TargetConsecutivo.ToString("000");
    }

    private class EvidenciaImportCandidate
    {
        public int SourceRowNumber { get; init; }
        public string? NumeroOficio { get; init; }
        public string? SourceFilePath { get; init; }
        public string? TipoEvidencia { get; init; }
        public string? NivelEducativo { get; init; }
        public string? FechaRaw { get; init; }
        public string? Notas { get; init; }
    }

    private class CaseImportTarget
    {
        public int OficioId { get; init; }
        public int CasoCorreccionId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string AuditoriaEstado { get; set; } = CatalogosAuditoriaCaso.Pendiente;
    }

    public class ImportError
    {
        public int RowNumber { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
