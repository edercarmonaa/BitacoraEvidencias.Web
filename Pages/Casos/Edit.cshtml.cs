using System.ComponentModel.DataAnnotations;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Casos;

public class EditModel(
    AppDbContext dbContext,
    ICasoApplicationService casoApplicationService) : PageModel
{
    public CasoHeader? Header { get; private set; }
    public IReadOnlyList<AuditorOption> AuditoresDisponibles { get; private set; } = [];
    public IReadOnlyList<string> EstatusDisponibles => CatalogosCaso.Estatus;
    public IReadOnlyList<string> NivelesEducativosDisponibles => CatalogosCaso.NivelesEducativos;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, int? returnOficioId = null, string? returnUrl = null)
    {
        var caso = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Include(x => x.Oficio)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (caso is null || caso.Oficio is null)
        {
            return NotFound();
        }

        Header = new CasoHeader
        {
            Id = caso.Id,
            OficioId = caso.OficioId,
            NumeroOficio = caso.Oficio.NumeroOficio,
            Consecutivo = caso.Consecutivo
        };

        Input = new InputModel
        {
            Id = caso.Id,
            Curp = caso.Curp,
            NombreCompleto = caso.NombreCompleto,
            Cct = caso.Cct,
            Validador = caso.Validador,
            AuditorUsuarioId = caso.AuditorUsuarioId,
            NivelEducativo = caso.NivelEducativo,
            TipoCorreccion = caso.TipoCorreccion,
            Folio = caso.Folio,
            PeriodoInicio = caso.PeriodoInicio,
            PeriodoFin = caso.PeriodoFin,
            FechaVerificacion = caso.FechaVerificacion,
            EvidenciaUrl = caso.EvidenciaUrl,
            Observaciones = caso.Observaciones,
            Estatus = caso.Estatus,
            ReturnOficioId = returnOficioId,
            ReturnUrl = NormalizeLocalReturnUrl(returnUrl)
        };

        AuditoresDisponibles = await LoadAuditoresDisponiblesAsync(caso.AuditorUsuarioId);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadHeaderAndAuditoresAsync(Input.Id, Input.AuditorUsuarioId);
            return Page();
        }

        var result = await casoApplicationService.UpdateAsync(new CasoUpdateRequest
        {
            Id = Input.Id,
            Curp = Input.Curp,
            NombreCompleto = Input.NombreCompleto,
            Cct = Input.Cct,
            Validador = Input.Validador,
            AuditorUsuarioId = Input.AuditorUsuarioId,
            NivelEducativo = Input.NivelEducativo,
            TipoCorreccion = Input.TipoCorreccion,
            Folio = Input.Folio,
            PeriodoInicio = Input.PeriodoInicio,
            PeriodoFin = Input.PeriodoFin,
            FechaVerificacion = Input.FechaVerificacion,
            EvidenciaUrl = Input.EvidenciaUrl,
            Observaciones = Input.Observaciones,
            Estatus = Input.Estatus
        });

        if (result.NotFound)
        {
            return NotFound();
        }

        foreach (var validationError in result.ValidationErrors)
        {
            var key = string.IsNullOrWhiteSpace(validationError.Key)
                ? string.Empty
                : $"{nameof(Input)}.{validationError.Key}";
            ModelState.AddModelError(key, validationError.Message);
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage);
        }

        if (result.HasValidationErrors || !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            await LoadHeaderAndAuditoresAsync(Input.Id, Input.AuditorUsuarioId);
            return Page();
        }

        TempData["FlashSuccess"] = $"Caso {result.ConsecutivoTexto} actualizado.";

        var targetUrl = ResolveReturnTargetUrl(result.CasoId);

        if (IsHtmxRequest() && !string.IsNullOrWhiteSpace(targetUrl))
        {
            Response.Headers["HX-Location"] = targetUrl;
            return new EmptyResult();
        }

        if (!string.IsNullOrWhiteSpace(Input.ReturnUrl))
        {
            return LocalRedirect(Input.ReturnUrl);
        }

        if (Input.ReturnOficioId.HasValue)
        {
            return RedirectToPage("/Oficios/Details", new { id = Input.ReturnOficioId.Value });
        }

        return RedirectToPage("./Details", new { id = result.CasoId });
    }

    private async Task<IReadOnlyList<AuditorOption>> LoadAuditoresDisponiblesAsync(int? selectedAuditorUsuarioId)
    {
        var options = await dbContext.UsuariosSistema
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Usuario)
            .Select(x => new AuditorOption
            {
                Id = x.Id,
                Usuario = x.Usuario
            })
            .ToListAsync();

        if (!selectedAuditorUsuarioId.HasValue || options.Any(x => x.Id == selectedAuditorUsuarioId.Value))
        {
            return options;
        }

        var currentAuditor = await dbContext.UsuariosSistema
            .AsNoTracking()
            .Where(x => x.Id == selectedAuditorUsuarioId.Value)
            .Select(x => new AuditorOption
            {
                Id = x.Id,
                Usuario = x.Usuario
            })
            .FirstOrDefaultAsync();

        if (currentAuditor is not null)
        {
            options.Insert(0, currentAuditor);
        }

        return options;
    }

    private async Task LoadHeaderAndAuditoresAsync(int caseId, int? selectedAuditorUsuarioId)
    {
        var caso = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Include(x => x.Oficio)
            .FirstOrDefaultAsync(x => x.Id == caseId);

        if (caso is null || caso.Oficio is null)
        {
            Header = null;
            AuditoresDisponibles = [];
            return;
        }

        Header = new CasoHeader
        {
            Id = caso.Id,
            OficioId = caso.OficioId,
            NumeroOficio = caso.Oficio.NumeroOficio,
            Consecutivo = caso.Consecutivo
        };

        AuditoresDisponibles = await LoadAuditoresDisponiblesAsync(selectedAuditorUsuarioId);
    }

    private bool IsHtmxRequest()
    {
        return string.Equals(Request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveReturnTargetUrl(int caseId)
    {
        if (!string.IsNullOrWhiteSpace(Input.ReturnUrl))
        {
            return Input.ReturnUrl;
        }

        return Input.ReturnOficioId.HasValue
            ? Url.Page("/Oficios/Details", new { id = Input.ReturnOficioId.Value })
            : Url.Page("./Details", new { id = caseId });
    }

    private string? NormalizeLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
    }

    public class CasoHeader
    {
        public int Id { get; init; }
        public int OficioId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string ConsecutivoTexto => Consecutivo.ToString("000");
    }

    public class InputModel
    {
        public int Id { get; set; }
        public int? ReturnOficioId { get; set; }
        public string? ReturnUrl { get; set; }

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

    public class AuditorOption
    {
        public int Id { get; init; }
        public string Usuario { get; init; } = string.Empty;
    }
}
