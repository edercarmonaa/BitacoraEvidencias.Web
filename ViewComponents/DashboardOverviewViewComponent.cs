using BitacoraEvidencias.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.ViewComponents;

public class DashboardOverviewViewComponent(AppDbContext dbContext) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int take = 6)
    {
        // Queries share this scoped DbContext and must complete before starting the next.
        var totalOficios = await dbContext.Oficios.AsNoTracking().CountAsync();
        var totalCasos = await dbContext.CasosCorreccion.AsNoTracking().CountAsync();
        var totalEvidencias = await dbContext.EvidenciasFoto.AsNoTracking().CountAsync();

        var oficiosRecientes = await dbContext.Oficios
            .AsNoTracking()
            .OrderByDescending(x => x.FechaOficio)
            .ThenByDescending(x => x.Id)
            .Select(x => new DashboardOficioItemModel
            {
                Id = x.Id,
                NumeroOficio = x.NumeroOficio,
                FechaOficio = x.FechaOficio,
                Asunto = x.Asunto,
                TotalCasos = x.Casos.Count
            })
            .Take(take)
            .ToListAsync();

        var casosPendientes = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => !x.Evidencias.Any() && (x.EvidenciaUrl == null || x.EvidenciaUrl == string.Empty))
            .OrderByDescending(x => x.Oficio!.FechaOficio)
            .ThenByDescending(x => x.Consecutivo)
            .Select(x => new DashboardCasoItemModel
            {
                Id = x.Id,
                NumeroOficio = x.Oficio!.NumeroOficio,
                Consecutivo = x.Consecutivo,
                NivelEducativo = x.NivelEducativo,
                TipoCorreccion = x.TipoCorreccion,
                Estatus = x.Estatus
            })
            .Take(take)
            .ToListAsync();


        var model = new DashboardOverviewViewModel
        {
            LastUpdatedLocal = DateTime.Now,
            StatCards =
            [
                new DashboardStatCardModel
                {
                    Title = "Oficios",
                    Value = totalOficios,
                    AccentClass = "border-start border-4 border-brand-guinda"
                },
                new DashboardStatCardModel
                {
                    Title = "Casos",
                    Value = totalCasos,
                    AccentClass = "border-start border-4 border-brand-arena"
                },
                new DashboardStatCardModel
                {
                    Title = "Evidencias",
                    Value = totalEvidencias,
                    AccentClass = "border-start border-4 border-brand-guinda-dark"
                }
            ],
            OficiosRecientes = oficiosRecientes,
            CasosPendientes = casosPendientes
        };

        return View(model);
    }
}
