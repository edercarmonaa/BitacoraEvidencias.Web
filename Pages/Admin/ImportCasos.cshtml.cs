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
public class ImportCasosModel(
    IImportPreviewStore importPreviewStore,
    ICasoImportApplicationService casoImportApplicationService,
    ICasoImportPreviewService casoImportPreviewService) : PageModel

{
    private const int DefaultPreviewPageSize = 20;
    private const int MaxPreviewPageSize = 100;
    private const string PreviewScope = "casos";

    private static readonly string[] ExpectedHeaders =
    [
        "Oficio",
        "Folio",
        "Cct",
        "PeriodoInicio",
        "PeriodoFin",
        "Curp",
        "NombreCompleto",
        "FechaVerificacion",
        "NIVEL",
        "TipoCorreccion",
        "Observaciones",
        "Estatus",
        "EvidenciaUrl",
        "Validador",
        "AuditorId"
    ];

    [BindProperty]
    public UploadInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPreviewPageSize;

    public CasoImportPreview? Preview { get; private set; }
    public IReadOnlyList<CasoImportRow> PreviewRowsPage { get; private set; } = [];
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
        var preview = await importPreviewStore.LoadAsync<CasoImportPreview>(PreviewScope, previewOwnerKey, cancellationToken);
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

        var result = await casoImportApplicationService.ImportAsync(preview, cancellationToken);
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
                FlashSuccess = $"Se importaron {result.ImportedCount} caso(s) correctamente desde {preview.FileName}.";
            }

            FlashError = $"Quedaron {result.FailedRows.Count} fila(s) pendientes. Se genero una nueva vista previa con los registros no importados.";
            return RedirectToPage();
        }

        await importPreviewStore.DeleteAsync(PreviewScope, previewOwnerKey, cancellationToken);
        FlashSuccess = $"Se importaron {result.ImportedCount} caso(s) correctamente desde {preview.FileName}.";
        return RedirectToPage();
    }

        private Task<CasoImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        return casoImportPreviewService.BuildPreviewAsync(file, cancellationToken);
    }

    private Task<CasoImportPreview> BuildPreviewFromRowsAsync(
        string fileName,
        IReadOnlyCollection<CasoImportRow> rows,
        CancellationToken cancellationToken)
    {
        return casoImportPreviewService.BuildPreviewFromRowsAsync(fileName, rows, cancellationToken);
    }

    private async Task LoadPreviewAsync(CancellationToken cancellationToken)
    {
        Preview = await importPreviewStore.LoadAsync<CasoImportPreview>(
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

    private void ApplyPreviewPaging(CasoImportPreview preview)
    {
        PageSize = ImportPreviewPagingSupport.NormalizePageSize(PageSize, DefaultPreviewPageSize, MaxPreviewPageSize);

        var paging = ImportPreviewPagingSupport.ApplyPaging(preview.Rows, PageNumber, PageSize, row => row.SourceRowNumber);
        PageNumber = paging.PageNumber;
        PreviewRowsTotalPages = paging.TotalPages;
        PreviewRowsPage = paging.Rows;
    }

    private static string? NormalizeCatalogValue(string? rawValue, IReadOnlyList<string> allowedValues)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var trimmed = rawValue.Trim();
        return allowedValues.FirstOrDefault(value => string.Equals(value, trimmed, StringComparison.OrdinalIgnoreCase));
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
                Message = "Los encabezados deben ser exactamente y en este orden: Oficio, Folio, Cct, PeriodoInicio, PeriodoFin, Curp, NombreCompleto, FechaVerificacion, NIVEL, TipoCorreccion, Observaciones, Estatus, EvidenciaUrl, Validador, AuditorId."
            });
            return;
        }

        for (var index = 0; index < ExpectedHeaders.Length; index++)
        {
            if (!string.Equals(normalizedHeaders[index], ExpectedHeaders[index], StringComparison.Ordinal))
            {
                errors.Add(new ImportError
                {
                    Message = "Los encabezados deben ser exactamente y en este orden: Oficio, Folio, Cct, PeriodoInicio, PeriodoFin, Curp, NombreCompleto, FechaVerificacion, NIVEL, TipoCorreccion, Observaciones, Estatus, EvidenciaUrl, Validador, AuditorId."
                });
                return;
            }
        }
    }

    private static string? GetColumnValue(TabularImportRow row, int index)
    {
        return index < row.Values.Count ? row.Values[index] : null;
    }

    public class UploadInput
    {
        [Display(Name = "Archivo")]
        public IFormFile? File { get; set; }
    }

    public class CasoImportPreview
    {
        public string FileName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int InvalidRows { get; set; }
        public List<CasoImportRow> Rows { get; set; } = [];
        public List<ImportError> Errors { get; set; } = [];
        public int ValidRows => Rows.Count;
        public bool HasBlockingErrors => Errors.Any(error => error.RowNumber <= 0);
        public bool CanImport => ValidRows > 0 && !HasBlockingErrors;
    }

    public class CasoImportRow
    {
        public int SourceRowNumber { get; set; }
        public string OficioNumero { get; set; } = string.Empty;
        public string? Folio { get; set; }
        public string Cct { get; set; } = string.Empty;
        public int PeriodoInicio { get; set; }
        public int PeriodoFin { get; set; }
        public string Curp { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public DateTime FechaVerificacion { get; set; }
        public string NivelEducativo { get; set; } = string.Empty;
        public string TipoCorreccion { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
        public string Estatus { get; set; } = string.Empty;
        public string? EvidenciaUrl { get; set; }
        public string? Validador { get; set; }
        public int AuditorUsuarioId { get; set; }
        public string AuditorUsuario { get; set; } = string.Empty;
    }

    private class CasoImportCandidate
    {
        public int SourceRowNumber { get; init; }
        public string? OficioNumero { get; init; }
        public string? Folio { get; init; }
        public string? Cct { get; init; }
        public string? PeriodoInicioRaw { get; init; }
        public string? PeriodoFinRaw { get; init; }
        public string? Curp { get; init; }
        public string? NombreCompleto { get; init; }
        public string? FechaVerificacionRaw { get; init; }
        public string? NivelRaw { get; init; }
        public string? TipoCorreccion { get; init; }
        public string? Observaciones { get; init; }
        public string? EstatusRaw { get; init; }
        public string? EvidenciaUrlRaw { get; init; }
        public string? Validador { get; init; }
        public string? AuditorIdRaw { get; init; }
    }

    private class OfficeImportTarget
    {
        public int OficioId { get; init; }
        public HashSet<string> ExistingLevels { get; init; } = new(StringComparer.Ordinal);
        public int NextConsecutivo { get; set; }
    }

    private class OfficeExistingCase
    {
        public int OficioId { get; init; }
        public int Consecutivo { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
    }

    public class ImportError
    {
        public int RowNumber { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}




