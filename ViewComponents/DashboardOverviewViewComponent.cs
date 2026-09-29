using BitacoraEvidencias.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.ViewComponents;

public class DashboardOverviewViewComponent(AppDbContext dbContext) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(int take = 6)
    {
        var totalOficiosTask = dbContext.Oficios.AsNoTracking().CountAsync();
        var totalCasosTask = dbContext.CasosCorreccion.AsNoTracking().CountAsync();
        var totalEvidenciasTask = dbContext.EvidenciasFoto.AsNoTracking().CountAsync();

        var oficiosRecientesTask = dbContext.Oficios
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

        var casosPendientesTask = dbContext.CasosCorreccion
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

        await Task.WhenAll(totalOficiosTask, totalCasosTask, totalEvidenciasTask, oficiosRecientesTask, casosPendientesTask);

        var model = new DashboardOverviewViewModel
        {
            LastUpdatedLocal = DateTime.Now,
            StatCards =
            [
                new DashboardStatCardModel
                {
                    Title = "Oficios",
                    Value = totalOficiosTask.Result,
                    AccentClass = "border-start border-4 border-brand-guinda"
                },
                new DashboardStatCardModel
                {
                    Title = "Casos",
                    Value = totalCasosTask.Result,
                    AccentClass = "border-start border-4 border-brand-arena"
                },
                new DashboardStatCardModel
                {
                    Title = "Evidencias",
                    Value = totalEvidenciasTask.Result,
                    AccentClass = "border-start border-4 border-brand-guinda-dark"
                }
            ],
            OficiosRecientes = oficiosRecientesTask.Result,
            CasosPendientes = casosPendientesTask.Result
        };

        return View(model);
    }
}
