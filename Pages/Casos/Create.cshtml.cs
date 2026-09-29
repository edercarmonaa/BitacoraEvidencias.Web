using System.ComponentModel.DataAnnotations;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Casos;

public class CreateModel(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : PageModel
{
    public OficioResumen? Oficio { get; private set; }
    public int SiguienteConsecutivo { get; private set; }
    public IReadOnlyList<AuditorOption> AuditoresDisponibles { get; private set; } = [];

    public IReadOnlyList<string> EstatusDisponibles => CatalogosCaso.Estatus;
    public IReadOnlyList<string> NivelesEducativosDisponibles => CatalogosCaso.NivelesEducativos;

    [BindProperty]
    public InputModel Input { get; set; } = new()
    {
        NivelEducativo = CatalogosCaso.NivelesEducativos[0],
        Estatus = CatalogosCaso.Estatus[0]
    };

    public async Task<IActionResult> OnGetAsync(int oficioId, int? returnOficioId = null)
    {
        var oficio = await LoadOficioAsync(oficioId);
        if (oficio is null)
        {
            return NotFound();
        }

        Oficio = oficio;
        Input.OficioId = oficioId;
        Input.ReturnOficioId = returnOficioId;
        AuditoresDisponibles = await LoadAuditoresDisponiblesAsync();
        SiguienteConsecutivo = await GetSiguienteConsecutivoAsync(oficioId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Oficio = await LoadOficioAsync(Input.OficioId);
        if (Oficio is null)
        {
            return NotFound();
        }

        var nivelEducativo = Input.NivelEducativo.Trim();
        if (Input.PeriodoInicio.HasValue && Input.PeriodoFin.HasValue && Input.PeriodoFin.Value < Input.PeriodoInicio.Value)
        {
            ModelState.AddModelError(nameof(Input.PeriodoFin), "El periodo fin no puede ser menor al periodo inicio.");
        }

        if (await ExistsCaseForLevelAsync(Input.OficioId, nivelEducativo))
        {
            ModelState.AddModelError(nameof(Input.NivelEducativo), $"Ya existe un caso de {nivelEducativo} para este oficio.");
        }

        if (Input.AuditorUsuarioId.HasValue && !await ActiveUserExistsAsync(Input.AuditorUsuarioId.Value))
        {
            ModelState.AddModelError(nameof(Input.AuditorUsuarioId), "Selecciona un auditor activo valido.");
        }

        if (!ModelState.IsValid)
        {
            AuditoresDisponibles = await LoadAuditoresDisponiblesAsync();
            SiguienteConsecutivo = await GetSiguienteConsecutivoAsync(Input.OficioId);
            return Page();
        }

        for (var intento = 0; intento < 3; intento++)
        {
            var siguiente = await GetSiguienteConsecutivoAsync(Input.OficioId);
            var caso = new CasoCorreccion
            {
                OficioId = Input.OficioId,
                Consecutivo = siguiente,
                Curp = NormalizeOptional(Input.Curp),
                NombreCompleto = NormalizeOptional(Input.NombreCompleto),
                Cct = NormalizeOptional(Input.Cct),
                Validador = NormalizeOptional(Input.Validador),
                AuditorUsuarioId = Input.AuditorUsuarioId,
                NivelEducativo = nivelEducativo,
                TipoCorreccion = Input.TipoCorreccion.Trim(),
                Folio = NormalizeOptional(Input.Folio),
                PeriodoInicio = Input.PeriodoInicio,
                PeriodoFin = Input.PeriodoFin,
                FechaVerificacion = Input.FechaVerificacion,
                EvidenciaUrl = NormalizeOptional(Input.EvidenciaUrl),
                Observaciones = NormalizeOptional(Input.Observaciones),
                Estatus = Input.Estatus.Trim()
            };

            dbContext.CasosCorreccion.Add(caso);

            try
            {
                await dbContext.SaveChangesAsync();
                await auditLogService.WriteAsync(new AuditLogEntry
                {
                    EventType = AuditEvents.Create,
                    EntityType = "Caso",
                    EntityId = caso.Id,
                    NumeroOficio = Oficio.NumeroOficio,
                    Consecutivo = caso.Consecutivo,
                    Curp = caso.Curp,
                    Cct = caso.Cct,
                    FolioCertificado = caso.Folio,
                    Details = "Alta de caso.",
                    Success = true
                });
                TempData["FlashSuccess"] = $"Caso {caso.ConsecutivoTexto} creado en oficio {Oficio.NumeroOficio}.";

                var targetUrl = Input.ReturnOficioId.HasValue
                    ? Url.Page("/Oficios/Details", new { id = Input.ReturnOficioId.Value })
                    : Url.Page("/Casos/Details", new { id = caso.Id });

                if (IsHtmxRequest() && !string.IsNullOrWhiteSpace(targetUrl))
                {
                    Response.Headers["HX-Location"] = targetUrl;
                    return new EmptyResult();
                }

                if (Input.ReturnOficioId.HasValue)
                {
                    return RedirectToPage("/Oficios/Details", new { id = Input.ReturnOficioId.Value });
                }

                return RedirectToPage("/Casos/Details", new { id = caso.Id });
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation("CasosCorreccion.OficioId, CasosCorreccion.Consecutivo"))
            {
                dbContext.Entry(caso).State = EntityState.Detached;
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation("CasosCorreccion.OficioId, CasosCorreccion.NivelEducativo"))
            {
                dbContext.Entry(caso).State = EntityState.Detached;
                ModelState.AddModelError(nameof(Input.NivelEducativo), $"Ya existe un caso de {nivelEducativo} para este oficio.");
                AuditoresDisponibles = await LoadAuditoresDisponiblesAsync();
                SiguienteConsecutivo = await GetSiguienteConsecutivoAsync(Input.OficioId);
                return Page();
            }
            catch (DbUpdateException)
            {
                dbContext.Entry(caso).State = EntityState.Detached;
                ModelState.AddModelError(string.Empty, "No se pudo guardar el caso. Intenta nuevamente.");
                AuditoresDisponibles = await LoadAuditoresDisponiblesAsync();
                SiguienteConsecutivo = await GetSiguienteConsecutivoAsync(Input.OficioId);
                return Page();
            }
        }

        ModelState.AddModelError(string.Empty, "No se pudo asignar el consecutivo del caso. Intenta nuevamente.");
        AuditoresDisponibles = await LoadAuditoresDisponiblesAsync();
        SiguienteConsecutivo = await GetSiguienteConsecutivoAsync(Input.OficioId);
        return Page();
    }

    private async Task<OficioResumen?> LoadOficioAsync(int oficioId)
    {
        return await dbContext.Oficios
            .AsNoTracking()
            .Where(x => x.Id == oficioId)
            .Select(x => new OficioResumen
            {
                Id = x.Id,
                NumeroOficio = x.NumeroOficio
            })
            .FirstOrDefaultAsync();
    }

    private async Task<int> GetSiguienteConsecutivoAsync(int oficioId)
    {
        var maximo = await dbContext.CasosCorreccion
            .Where(x => x.OficioId == oficioId)
            .MaxAsync(x => (int?)x.Consecutivo) ?? 0;

        return maximo + 1;
    }

    private async Task<bool> ExistsCaseForLevelAsync(int oficioId, string nivelEducativo)
    {
        return await dbContext.CasosCorreccion
            .AsNoTracking()
            .AnyAsync(x => x.OficioId == oficioId && x.NivelEducativo == nivelEducativo);
    }

    private async Task<IReadOnlyList<AuditorOption>> LoadAuditoresDisponiblesAsync()
    {
        return await dbContext.UsuariosSistema
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Usuario)
            .Select(x => new AuditorOption
            {
                Id = x.Id,
                Usuario = x.Usuario
            })
            .ToListAsync();
    }

    private async Task<bool> ActiveUserExistsAsync(int userId)
    {
        return await dbContext.UsuariosSistema
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.Activo);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private bool IsHtmxRequest()
    {
        return string.Equals(Request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
    }

    public class InputModel
    {
        public int OficioId { get; set; }
        public int? ReturnOficioId { get; set; }

        [StringLength(18)]
        [Display(Name = "CURP")]
        public string? Curp { get; set; }

        [StringLength(140)]
        [Display(Name = "Nombre completo")]
        public string? NombreCompleto { get; set; }

        [StringLength(30)]
        [Display(Name = "CCT")]
        public string? Cct { get; set; }

        [StringLength(140)]
        [Display(Name = "Validador")]
        public string? Validador { get; set; }

        [Display(Name = "Auditor")]
        public int? AuditorUsuarioId { get; set; }

        [Required(ErrorMessage = "Selecciona un nivel educativo.")]
        [CustomValidation(typeof(CatalogosCaso), nameof(CatalogosCaso.ValidateNivelEducativo))]
        [Display(Name = "Nivel educativo")]
        public string NivelEducativo { get; set; } = CatalogosCaso.NivelesEducativos[0];

        [Required(ErrorMessage = "El tipo de correccion es obligatorio.")]
        [StringLength(80)]
        [Display(Name = "Tipo de correccion")]
        public string TipoCorreccion { get; set; } = string.Empty;

        [StringLength(80)]
        [Display(Name = "Folio certificado")]
        public string? Folio { get; set; }

        [Display(Name = "Periodo inicio")]
        [Range(1900, 2100, ErrorMessage = "Captura un ano de inicio valido.")]
        public int? PeriodoInicio { get; set; }

        [Display(Name = "Periodo fin")]
        [Range(1900, 2100, ErrorMessage = "Captura un ano de fin valido.")]
        public int? PeriodoFin { get; set; }

        [Display(Name = "Fecha de verificacion")]
        [DataType(DataType.Date)]
        public DateTime? FechaVerificacion { get; set; }

        [Display(Name = "Evidencia (liga)")]
        [Url(ErrorMessage = "Captura una URL valida (http/https).")]
        [StringLength(1000)]
        public string? EvidenciaUrl { get; set; }

        [Display(Name = "Observaciones")]
        public string? Observaciones { get; set; }

        [Required(ErrorMessage = "Selecciona un estatus.")]
        [CustomValidation(typeof(CatalogosCaso), nameof(CatalogosCaso.ValidateEstatus))]
        [Display(Name = "Estatus")]
        public string Estatus { get; set; } = CatalogosCaso.Estatus[0];
    }

    public class OficioResumen
    {
        public int Id { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
    }

    public class AuditorOption
    {
        public int Id { get; init; }
        public string Usuario { get; init; } = string.Empty;
    }
}
