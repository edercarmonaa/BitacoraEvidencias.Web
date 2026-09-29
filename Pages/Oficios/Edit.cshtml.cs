using System.ComponentModel.DataAnnotations;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Oficios;

public class EditModel(
    AppDbContext dbContext,
    IOficioApplicationService oficioApplicationService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var oficio = await dbContext.Oficios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (oficio is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = oficio.Id,
            NumeroOficio = oficio.NumeroOficio,
            FechaOficio = oficio.FechaOficio,
            Asunto = oficio.Asunto,
            Notas = oficio.Notas
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await oficioApplicationService.UpdateAsync(new OficioUpdateRequest
        {
            Id = Input.Id,
            NumeroOficio = Input.NumeroOficio,
            FechaOficio = Input.FechaOficio,
            Asunto = Input.Asunto,
            Notas = Input.Notas
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
            return Page();
        }

        TempData["FlashSuccess"] = $"Oficio {result.NumeroOficio} actualizado.";

        var detailsUrl = Url.Page("./Details", new { id = result.OficioId }) ?? $"/Oficios/Details/{result.OficioId}";
        if (IsHtmxRequest())
        {
            Response.Headers["HX-Redirect"] = detailsUrl;
            return new EmptyResult();
        }

        return RedirectToPage("./Details", new { id = result.OficioId });
    }

    private bool IsHtmxRequest()
    {
        return string.Equals(Request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
    }

    public class InputModel
    {
        public int Id { get; set; }

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
