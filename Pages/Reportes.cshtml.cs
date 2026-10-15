using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.ViewComponents;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages;

public class ReportesModel(AppDbContext dbContext) : PageModel
{
    private const int PendientesPreviewLimit = 5;
    private const int IncorrectosPreviewLimit = 5;
    private const int TiposCorreccionPreviewLimit = 5;
    private const int NoCompletadosPreviewLimit = 5;

    public int TotalOficios { get; private set; }
    public int TotalCasos { get; private set; }
    public int TotalEvidencias { get; private set; }
    public int TotalPendientesEvidencia { get; private set; }
    public int TotalOficiosAuditadosCorrectos { get; private set; }
    public int TotalOficiosConEvidenciaIncorrecta { get; private set; }
    public int TotalCasosConEvidenciaIncorrecta { get; private set; }

    public List<PendienteEvidenciaRow> PendientesEvidencia { get; private set; } = [];
    public List<IncorrectAuditOfficeRow> OficiosConEvidenciaIncorrecta { get; private set; } = [];
    public List<IncorrectAuditCaseRow> CasosConEvidenciaIncorrecta { get; private set; } = [];
    public List<TipoCorreccionRow> CasosPorTipoCorreccion { get; private set; } = [];
    public int TotalTiposCorreccionCompletados { get; private set; }
    public List<CasoNoCompletadoRow> CasosNoCompletados { get; private set; } = [];
    public int TotalCasosNoCompletados { get; private set; }
    public IReadOnlyList<DashboardStatCardModel> StatCards { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        TotalOficios = await dbContext.Oficios.CountAsync(cancellationToken);
        TotalCasos = await dbContext.CasosCorreccion.CountAsync(cancellationToken);
        TotalEvidencias = await dbContext.EvidenciasFoto.CountAsync(cancellationToken);
        TotalPendientesEvidencia = await dbContext.CasosCorreccion
            .AsNoTracking()
            .CountAsync(x => !x.Evidencias.Any() && (x.EvidenciaUrl == null || x.EvidenciaUrl == string.Empty), cancellationToken);

        PendientesEvidencia = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => !x.Evidencias.Any() && (x.EvidenciaUrl == null || x.EvidenciaUrl == string.Empty))
            .OrderByDescending(x => x.CreadoEn)
            .ThenByDescending(x => x.Id)
            .Select(x => new PendienteEvidenciaRow
            {
                CasoId = x.Id,
                NumeroOficio = x.Oficio!.NumeroOficio,
                Consecutivo = x.Consecutivo,
                Curp = x.Curp,
                NivelEducativo = x.NivelEducativo,
                TipoCorreccion = x.TipoCorreccion,
                Estatus = x.Estatus
            })
            .Take(PendientesPreviewLimit)
            .ToListAsync(cancellationToken);

        var oficiosPorTipoCorreccionQuery = dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.Estatus == "Completado")
            .GroupBy(x => x.TipoCorreccion)
            .Select(group => new TipoCorreccionRow
            {
                TipoCorreccion = group.Key,
                Total = group.Select(x => x.OficioId).Distinct().Count()
            });

        TotalTiposCorreccionCompletados = await oficiosPorTipoCorreccionQuery.CountAsync(cancellationToken);
        CasosPorTipoCorreccion = await oficiosPorTipoCorreccionQuery
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.TipoCorreccion)
            .Take(TiposCorreccionPreviewLimit)
            .ToListAsync(cancellationToken);

        var casosNoCompletadosQuery = dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.Estatus != "Completado");

        TotalCasosNoCompletados = await casosNoCompletadosQuery.CountAsync(cancellationToken);
        CasosNoCompletados = await casosNoCompletadosQuery
            .OrderByDescending(x => x.CreadoEn)
            .ThenByDescending(x => x.Id)
            .Select(x => new CasoNoCompletadoRow
            {
                CasoId = x.Id,
                NumeroOficio = x.Oficio!.NumeroOficio,
                Consecutivo = x.Consecutivo,
                Curp = x.Curp,
                NivelEducativo = x.NivelEducativo,
                TipoCorreccion = x.TipoCorreccion,
                Estatus = x.Estatus
            })
            .Take(NoCompletadosPreviewLimit)
            .ToListAsync(cancellationToken);

        TotalOficiosAuditadosCorrectos = await dbContext.Oficios.AsNoTracking()
            .CountAsync(o => o.Casos.Any() &&
                o.Casos.All(c => c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl != string.Empty)) &&
                o.Casos.All(c => c.AuditoriaEstado != null && c.AuditoriaEstado.Trim() == CatalogosAuditoriaCaso.Coincidente), cancellationToken);

        var incorrectAuditOffices = IncorrectAuditOfficeQueries.Offices(dbContext);
        TotalOficiosConEvidenciaIncorrecta = await incorrectAuditOffices.CountAsync(cancellationToken);
        OficiosConEvidenciaIncorrecta = await incorrectAuditOffices.Take(IncorrectosPreviewLimit).ToListAsync(cancellationToken);
        var incorrectAuditCases = IncorrectAuditOfficeQueries.Cases(dbContext);
        TotalCasosConEvidenciaIncorrecta = await incorrectAuditCases.CountAsync(cancellationToken);
        CasosConEvidenciaIncorrecta = await incorrectAuditCases.Take(IncorrectosPreviewLimit).ToListAsync(cancellationToken);
        StatCards =
        [
            new DashboardStatCardModel
            {
                Title = "Oficios",
                Value = TotalOficios,
                AccentClass = "border-start border-4 border-brand-guinda"
            },
            new DashboardStatCardModel
            {
                Title = "Casos",
                Value = TotalCasos,
                AccentClass = "border-start border-4 border-brand-arena"
            },
            new DashboardStatCardModel
            {
                Title = "Evidencias",
                Value = TotalEvidencias,
                AccentClass = "border-start border-4 border-brand-guinda-dark"
            },
            new DashboardStatCardModel
            {
                Title = "Oficios auditados correctos",
                Value = TotalOficiosAuditadosCorrectos,
                AccentClass = "border-start border-4 border-success"
            },
            new DashboardStatCardModel
            {
                Title = "Oficios con evidencia incorrecta",
                Value = TotalOficiosConEvidenciaIncorrecta,
                AccentClass = "border-start border-4 border-danger"
            }
        ];
    }

    public class PendienteEvidenciaRow
    {
        public int CasoId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public string Estatus { get; init; } = string.Empty;
        public string ConsecutivoTexto => Consecutivo.ToString("000");
    }

    public class TipoCorreccionRow
    {
        public string TipoCorreccion { get; init; } = string.Empty;
        public int Total { get; init; }
    }

    public class CasoNoCompletadoRow
    {
        public int CasoId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public string Estatus { get; init; } = string.Empty;
        public string ConsecutivoTexto => Consecutivo.ToString("000");
    }
}
