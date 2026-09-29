using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Oficios;

public class DeleteModel(
    AppDbContext dbContext,
    IOficioApplicationService oficioApplicationService) : PageModel
{
    public OficioDeleteView? Oficio { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Oficio = await dbContext.Oficios
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new OficioDeleteView
            {
                Id = x.Id,
                NumeroOficio = x.NumeroOficio,
                FechaOficio = x.FechaOficio,
                Asunto = x.Asunto,
                TotalCasos = x.Casos.Count,
                TotalEvidencias = x.Casos.SelectMany(c => c.Evidencias).Count()
            })
            .FirstOrDefaultAsync();

        if (Oficio is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var result = await oficioApplicationService.DeleteAsync(id);
        if (result.NotFound)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            TempData["FlashError"] = result.ErrorMessage;
            return RedirectToPage("./Details", new { id });
        }

        if (!string.IsNullOrWhiteSpace(result.WarningMessage))
        {
            TempData["FlashError"] = result.WarningMessage;
        }

        TempData["FlashSuccess"] = $"Oficio {result.NumeroOficio} eliminado correctamente.";

        var indexUrl = Url.Page("./Index") ?? "/Oficios/Index";
        if (IsHtmxRequest())
        {
            Response.Headers["HX-Redirect"] = indexUrl;
            return new EmptyResult();
        }

        return RedirectToPage("./Index");
    }

    private bool IsHtmxRequest()
    {
        return string.Equals(Request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
    }

    public class OficioDeleteView
    {
        public int Id { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public DateTime FechaOficio { get; init; }
        public string Asunto { get; init; } = string.Empty;
        public int TotalCasos { get; init; }
        public int TotalEvidencias { get; init; }
    }
}
