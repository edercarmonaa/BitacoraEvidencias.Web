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
public class ImportOficiosModel(
    IImportPreviewStore importPreviewStore,
    IOficioImportApplicationService oficioImportApplicationService,
    IOficioImportPreviewService oficioImportPreviewService) : PageModel

{
    private const int DefaultPreviewPageSize = 20;
    private const int MaxPreviewPageSize = 100;
    private const string PreviewScope = "oficios";

    private static readonly string[] ExpectedHeaders =
    [
        "NumeroOficio",
        "FechaOficio",
        "Asunto",
        "Notas"
    ];

    [BindProperty]
    public UploadInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPreviewPageSize;

    public OficioImportPreview? Preview { get; private set; }
    public IReadOnlyList<OficioImportRow> PreviewRowsPage { get; private set; } = [];
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
        var preview = await importPreviewStore.LoadAsync<OficioImportPreview>(PreviewScope, previewOwnerKey, cancellationToken);
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

        var result = await oficioImportApplicationService.ImportAsync(preview, cancellationToken);
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
                FlashSuccess = $"Se importaron {result.ImportedCount} oficio(s) correctamente desde {preview.FileName}.";
            }

            FlashError = $"Quedaron {result.FailedRows.Count} fila(s) pendientes. Se genero una nueva vista previa con los registros no importados.";
            return RedirectToPage();
        }

        await importPreviewStore.DeleteAsync(PreviewScope, previewOwnerKey, cancellationToken);
        FlashSuccess = $"Se importaron {result.ImportedCount} oficio(s) correctamente desde {preview.FileName}.";
        return RedirectToPage();
    }

        private Task<OficioImportPreview> BuildPreviewAsync(IFormFile file, CancellationToken cancellationToken)
    {
        return oficioImportPreviewService.BuildPreviewAsync(file, cancellationToken);
    }

    private Task<OficioImportPreview> BuildPreviewFromRowsAsync(
        string fileName,
        IReadOnlyCollection<OficioImportRow> rows,
        CancellationToken cancellationToken)
    {
        return oficioImportPreviewService.BuildPreviewFromRowsAsync(fileName, rows, cancellationToken);
    }

    private async Task LoadPreviewAsync(CancellationToken cancellationToken)
    {
        Preview = await importPreviewStore.LoadAsync<OficioImportPreview>(
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

    private void ApplyPreviewPaging(OficioImportPreview preview)
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
                Message = "Los encabezados deben ser exactamente y en este orden: NumeroOficio, FechaOficio, Asunto, Notas."
            });
            return;
        }

        for (var index = 0; index < ExpectedHeaders.Length; index++)
        {
            if (!string.Equals(normalizedHeaders[index], ExpectedHeaders[index], StringComparison.Ordinal))
            {
                errors.Add(new ImportError
                {
                    Message = "Los encabezados deben ser exactamente y en este orden: NumeroOficio, FechaOficio, Asunto, Notas."
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

    public class OficioImportPreview
    {
        public string FileName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int InvalidRows { get; set; }
        public List<OficioImportRow> Rows { get; set; } = [];
        public List<ImportError> Errors { get; set; } = [];
        public int ValidRows => Rows.Count;
        public bool HasBlockingErrors => Errors.Any(error => error.RowNumber <= 0);
        public bool CanImport => ValidRows > 0 && !HasBlockingErrors;
    }

    public class OficioImportRow
    {
        public int SourceRowNumber { get; set; }
        public string NumeroOficio { get; set; } = string.Empty;
        public DateTime FechaOficio { get; set; }
        public string Asunto { get; set; } = string.Empty;
        public string? Notas { get; set; }
    }

    private class OficioImportCandidate
    {
        public int SourceRowNumber { get; init; }
        public string? NumeroOficio { get; init; }
        public string? FechaRaw { get; init; }
        public string? Asunto { get; init; }
        public string? Notas { get; init; }
    }

    public class ImportError
    {
        public int RowNumber { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}



