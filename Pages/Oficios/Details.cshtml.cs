using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Oficios;

public class DetailsModel(AppDbContext dbContext) : PageModel
{
    public Oficio? Oficio { get; private set; }
    public List<CasoRow> Casos { get; private set; } = [];

    [TempData]
    public string? FlashSuccess { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Oficio = await dbContext.Oficios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (Oficio is null)
        {
            return NotFound();
        }

        Casos = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.OficioId == id)
            .OrderBy(x => x.Consecutivo)
            .Select(x => new CasoRow
            {
                Id = x.Id,
                Consecutivo = x.Consecutivo,
                Curp = x.Curp,
                NombreCompleto = x.NombreCompleto,
                NivelEducativo = x.NivelEducativo,
                TipoCorreccion = x.TipoCorreccion,
                Estatus = x.Estatus,
                TotalEvidencias = x.Evidencias.Count
            })
            .ToListAsync();

        return Page();
    }

    public class CasoRow
    {
        public int Id { get; init; }
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string? NombreCompleto { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public string Estatus { get; init; } = string.Empty;
        public int TotalEvidencias { get; init; }

        public string ConsecutivoTexto => Consecutivo.ToString("000");
    }
}
