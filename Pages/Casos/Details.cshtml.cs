using System.ComponentModel.DataAnnotations;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Casos;

public class DetailsModel(
    AppDbContext dbContext,
    IPhotoStorageService photoStorageService,
    IAuditLogService auditLogService,
    ICaseAuditService caseAuditService) : PageModel
{
    public CasoCorreccion? Caso { get; private set; }
    public List<EvidenciaRow> Evidencias { get; private set; } = [];

    public IReadOnlyList<string> TiposEvidencia => CatalogosCaso.TiposEvidencia;

    [BindProperty]
    public UploadInputModel Input { get; set; } = new()
    {
        TipoEvidencia = CatalogosCaso.TiposEvidencia[0],
        FechaEvidencia = DateTime.Today
    };

    [TempData]
    public string? FlashSuccess { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadPageAsync(id);
    }

    public async Task<IActionResult> OnPostUploadAsync(int id)
    {
        var archivos = Input.Archivos ?? [];
        var archivosGuardados = new List<StoredPhotoInfo>();
        if (archivos.Count == 0 || archivos.All(x => x.Length == 0))
        {
            ModelState.AddModelError("Input.Archivos", "Selecciona al menos una imagen.");
        }

        if (!ModelState.IsValid)
        {
            return await LoadPageAsync(id);
        }

        var caso = await dbContext.CasosCorreccion
            .Include(x => x.Oficio)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (caso is null || caso.Oficio is null)
        {
            return NotFound();
        }

        var totalGuardadas = 0;
        var fechaEvidencia = Input.FechaEvidencia == default ? DateTime.Today : Input.FechaEvidencia.Date;
        var tipoEvidencia = string.IsNullOrWhiteSpace(Input.TipoEvidencia)
            ? CatalogosCaso.TiposEvidencia[0]
            : Input.TipoEvidencia.Trim();

        foreach (var archivo in archivos.Where(x => x.Length > 0))
        {
            StoredPhotoInfo resultado;
            try
            {
                resultado = await photoStorageService.SaveEvidenceAsync(
                    archivo,
                    caso.Oficio.NumeroOficio,
                    caso.Consecutivo);
            }
            catch (InvalidOperationException ex)
            {
                CleanupStoredFiles(archivosGuardados);
                ModelState.AddModelError("Input.Archivos", ex.Message);
                return await LoadPageAsync(id);
            }

            archivosGuardados.Add(resultado);

            dbContext.EvidenciasFoto.Add(new EvidenciaFoto
            {
                CasoCorreccionId = caso.Id,
                TipoEvidencia = tipoEvidencia,
                FechaEvidencia = fechaEvidencia,
                Notas = NormalizeOptional(Input.Notas),
                RutaArchivo = resultado.RelativeFilePath,
                RutaMiniatura = resultado.RelativeThumbnailPath
            });

            totalGuardadas++;
        }

        if (totalGuardadas == 0)
        {
            ModelState.AddModelError("Input.Archivos", "No se detectaron archivos válidos para guardar.");
            return await LoadPageAsync(id);
        }

        var auditResetApplied = caseAuditService.ResetToPending(caso);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            CleanupStoredFiles(archivosGuardados);
            ModelState.AddModelError(string.Empty, "No fue posible guardar las evidencias. Intenta nuevamente.");
            return await LoadPageAsync(id);
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.UploadEvidence,
            EntityType = "Caso",
            EntityId = caso.Id,
            NumeroOficio = caso.Oficio.NumeroOficio,
            Consecutivo = caso.Consecutivo,
            Curp = caso.Curp,
            Cct = caso.Cct,
            FolioCertificado = caso.Folio,
            EvidenciasCount = totalGuardadas,
            Details = $"Carga de evidencia. Archivos guardados: {totalGuardadas}.",
            Success = true
        });

        if (auditResetApplied)
        {
            await caseAuditService.WriteResetLogAsync(
                caso,
                $"La auditoria regreso a pendiente por carga de {totalGuardadas} evidencia(s).");
        }

        FlashSuccess = $"Se guardaron {totalGuardadas} evidencia(s).";

        if (IsHtmxRequest())
        {
            Input = new UploadInputModel
            {
                TipoEvidencia = CatalogosCaso.TiposEvidencia[0],
                FechaEvidencia = DateTime.Today
            };
            return await LoadPageAsync(id);
        }

        return RedirectToPage(new { id });
    }

    public string ToEvidenceUrl(int evidenciaId, bool thumbnail)
    {
        var baseUrl = Url.Page("/Evidencias/View", new { id = evidenciaId }) ?? $"/Evidencias/View/{evidenciaId}";
        var thumbQuery = thumbnail ? "&thumb=true" : string.Empty;
        return baseUrl + $"?v={evidenciaId}{thumbQuery}";
    }

    private async Task<IActionResult> LoadPageAsync(int id)
    {
        Caso = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Include(x => x.Oficio)
            .Include(x => x.AuditorUsuario)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Caso is null || Caso.Oficio is null)
        {
            return NotFound();
        }

        Evidencias = await dbContext.EvidenciasFoto
            .AsNoTracking()
            .Where(x => x.CasoCorreccionId == id)
            .OrderByDescending(x => x.FechaEvidencia)
            .ThenByDescending(x => x.Id)
            .Select(x => new EvidenciaRow
            {
                Id = x.Id,
                TipoEvidencia = x.TipoEvidencia,
                FechaEvidencia = x.FechaEvidencia,
                Notas = x.Notas
            })
            .ToListAsync();

        if (Input.FechaEvidencia == default)
        {
            Input.FechaEvidencia = DateTime.Today;
        }

        if (string.IsNullOrWhiteSpace(Input.TipoEvidencia))
        {
            Input.TipoEvidencia = CatalogosCaso.TiposEvidencia[0];
        }

        return Page();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private bool IsHtmxRequest()
    {
        return string.Equals(Request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private void CleanupStoredFiles(IEnumerable<StoredPhotoInfo> storedFiles)
    {
        foreach (var storedFile in storedFiles)
        {
            photoStorageService.DeleteEvidenceFiles(storedFile.RelativeFilePath, storedFile.RelativeThumbnailPath);
        }
    }

    public class UploadInputModel
    {
        [Required(ErrorMessage = "Selecciona el tipo de evidencia.")]
        [CustomValidation(typeof(CatalogosCaso), nameof(CatalogosCaso.ValidateTipoEvidencia))]
        [Display(Name = "Tipo de evidencia")]
        public string TipoEvidencia { get; set; } = CatalogosCaso.TiposEvidencia[0];

        [Display(Name = "Fecha evidencia")]
        [DataType(DataType.Date)]
        public DateTime FechaEvidencia { get; set; } = DateTime.Today;

        [Display(Name = "Notas")]
        public string? Notas { get; set; }

        [Display(Name = "Archivos")]
        public List<IFormFile> Archivos { get; set; } = [];
    }

    public class EvidenciaRow
    {
        public int Id { get; init; }
        public string TipoEvidencia { get; init; } = string.Empty;
        public DateTime FechaEvidencia { get; init; }
        public string? Notas { get; init; }
    }
}
