using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Casos;

public class DeleteModel(
    AppDbContext dbContext,
    ICasoApplicationService casoApplicationService) : PageModel
{
    public int? ReturnOficioId { get; set; }
    public string? ReturnUrl { get; set; }
    public CasoDeleteView? Caso { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, int? returnOficioId = null, string? returnUrl = null)
    {
        ReturnOficioId = returnOficioId;
        ReturnUrl = NormalizeLocalReturnUrl(returnUrl);
        Caso = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CasoDeleteView
            {
                Id = x.Id,
                OficioId = x.OficioId,
                NumeroOficio = x.Oficio!.NumeroOficio,
                Consecutivo = x.Consecutivo,
                Curp = x.Curp,
                NombreCompleto = x.NombreCompleto,
                NivelEducativo = x.NivelEducativo,
                TipoCorreccion = x.TipoCorreccion,
                TotalEvidencias = x.Evidencias.Count
            })
            .FirstOrDefaultAsync();

        if (Caso is null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, int? returnOficioId = null, string? returnUrl = null)
    {
        ReturnUrl = NormalizeLocalReturnUrl(returnUrl);
        var result = await casoApplicationService.DeleteAsync(id);
        if (result.NotFound)
        {
            return NotFound();
        }

        var targetId = returnOficioId ?? result.RedirectOficioId;
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            TempData["FlashError"] = result.ErrorMessage;
            var failTargetUrl = ResolveReturnTargetUrl(targetId);
            if (IsHtmxRequest() && !string.IsNullOrWhiteSpace(failTargetUrl))
            {
                Response.Headers["HX-Location"] = failTargetUrl;
                return new EmptyResult();
            }

            return RedirectToResolvedTarget(targetId);
        }

        if (!string.IsNullOrWhiteSpace(result.WarningMessage))
        {
            TempData["FlashError"] = result.WarningMessage;
        }

        TempData["FlashSuccess"] = $"Caso {result.ConsecutivoTexto} eliminado correctamente.";
        var targetUrl = ResolveReturnTargetUrl(targetId);
        if (IsHtmxRequest() && !string.IsNullOrWhiteSpace(targetUrl))
        {
            Response.Headers["HX-Location"] = targetUrl;
            return new EmptyResult();
        }

        return RedirectToResolvedTarget(targetId);
    }

    private bool IsHtmxRequest()
    {
        return string.Equals(Request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase);
    }

    private string? NormalizeLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
    }

    private string? ResolveReturnTargetUrl(int fallbackOficioId)
    {
        return !string.IsNullOrWhiteSpace(ReturnUrl)
            ? ReturnUrl
            : Url.Page("/Oficios/Details", new { id = fallbackOficioId });
    }

    private IActionResult RedirectToResolvedTarget(int fallbackOficioId)
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Oficios/Details", new { id = fallbackOficioId });
    }

    public class CasoDeleteView
    {
        public int Id { get; init; }
        public int OficioId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string? NombreCompleto { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public int TotalEvidencias { get; init; }
        public string ConsecutivoTexto => Consecutivo.ToString("000");
    }
}
