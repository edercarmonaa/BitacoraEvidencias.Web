using System.ComponentModel.DataAnnotations;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Oficios;

public class CreateModel(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new()
    {
        FechaOficio = DateTime.Today
    };

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var numeroNormalizado = Input.NumeroOficio.Trim();
        var existingNumbers = await dbContext.Oficios
            .AsNoTracking()
            .Select(x => x.NumeroOficio)
            .ToListAsync();

        if (existingNumbers.Contains(numeroNormalizado, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(Input.NumeroOficio), "Ya existe un oficio con ese numero.");
            return Page();
        }

        if (OficioStoragePath.TryFindFolderCollision(numeroNormalizado, existingNumbers, out var conflictingNumero))
        {
            ModelState.AddModelError(
                nameof(Input.NumeroOficio),
                $"El numero de oficio colisiona con la carpeta de '{conflictingNumero}'. Usa un valor que produzca una carpeta distinta.");
            return Page();
        }

        var oficio = new Oficio
        {
            NumeroOficio = numeroNormalizado,
            FechaOficio = Input.FechaOficio,
            Asunto = Input.Asunto.Trim(),
            Notas = string.IsNullOrWhiteSpace(Input.Notas) ? null : Input.Notas.Trim()
        };

        dbContext.Oficios.Add(oficio);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation("Oficios.NumeroOficio"))
        {
            ModelState.AddModelError(nameof(Input.NumeroOficio), "Ya existe un oficio con ese numero.");
            return Page();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo guardar el oficio. Intenta nuevamente.");
            return Page();
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Create,
            EntityType = "Oficio",
            EntityId = oficio.Id,
            NumeroOficio = oficio.NumeroOficio,
            Details = "Alta de oficio.",
            Success = true
        });

        TempData["FlashSuccess"] = $"Oficio {oficio.NumeroOficio} registrado correctamente.";

        var detailsUrl = Url.Page("./Details", new { id = oficio.Id }) ?? $"/Oficios/Details/{oficio.Id}";
        if (IsHtmxRequest())
        {
            Response.Headers["HX-Redirect"] = detailsUrl;
            return new EmptyResult();
        }

        return RedirectToPage("./Details", new { id = oficio.Id });
    }

    private bool IsHtmxRequest()
    {
        return string.Equals(Request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "El numero de oficio es obligatorio.")]
        [StringLength(80)]
        [Display(Name = "Numero de oficio")]
        public string NumeroOficio { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime FechaOficio { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "El asunto es obligatorio.")]
        [StringLength(200)]
        [Display(Name = "Asunto")]
        public string Asunto { get; set; } = string.Empty;

        [Display(Name = "Notas")]
        [StringLength(255, ErrorMessage = "Notas excede 255 caracteres.")]
        public string? Notas { get; set; }
    }
}
