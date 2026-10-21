using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.ViewComponents;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages;

public class ReportesModel(AppDbContext dbContext, ReportReadService? reports = null) : PageModel
{
    public DateTime? SummaryUpdatedAtUtc { get; private set; }
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
        reports ??= new ReportReadService(dbContext, new ConfigurationBuilder().Build());
        await reports.InitializeAsync(cancellationToken);
        SummaryUpdatedAtUtc = reports.UpdatedAtUtc;
        var officeTotals = await reports.Offices.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(), Correctos = g.Count(o => o.Correcto), Incorrectos = g.Count(o => o.Incorrecto)
        }).SingleOrDefaultAsync(cancellationToken);
        TotalOficios = officeTotals?.Total ?? 0;
        TotalOficiosAuditadosCorrectos = officeTotals?.Correctos ?? 0;
        TotalOficiosConEvidenciaIncorrecta = officeTotals?.Incorrectos ?? 0;
        // One aggregate over the small read model replaces repeated evidence scans.
        var totals = await reports.Cases.GroupBy(_ => 1).Select(g => new
        {
            Casos = g.Count(), Fotos = g.Sum(c => c.Fotos),
            Pendientes = g.Count(c => c.Fotos == 0 && !c.TieneUrl),
            NoCompletados = g.Count(c => c.Estatus != "Completado"),
            Incorrectos = g.Count(c => c.Incorrecto)
        }).SingleOrDefaultAsync(cancellationToken);
        TotalCasos = totals?.Casos ?? 0;
        TotalEvidencias = totals?.Fotos ?? 0;
        TotalPendientesEvidencia = totals?.Pendientes ?? 0;
        TotalCasosNoCompletados = totals?.NoCompletados ?? 0;
        TotalCasosConEvidenciaIncorrecta = totals?.Incorrectos ?? 0;

        PendientesEvidencia = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => !x.Evidencias.Any() && (x.EvidenciaUrl == null || x.EvidenciaUrl.Length == 0))
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

        var oficiosPorTipoCorreccionQuery = reports.Cases
            .Where(x => x.Estatus == "Completado")
            .Select(x => new { x.TipoCorreccion, x.OficioId }).Distinct()
            .GroupBy(x => x.TipoCorreccion)
            .Select(group => new TipoCorreccionRow
            {
                TipoCorreccion = group.Key,
                Total = group.Count()
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

        var incorrectAuditOffices = reports.UsesSummary
            ? dbContext.Oficios.AsNoTracking()
                .Where(o => reports.Offices.Where(r => r.Incorrecto).Select(r => r.Id).Contains(o.Id))
                .Select(o => new IncorrectAuditOfficeRow
                {
                    Id = o.Id, NumeroOficio = o.NumeroOficio, FechaOficio = o.FechaOficio,
                    Asunto = o.Asunto, TotalCasos = o.Casos.Count
                }).OrderByDescending(o => o.FechaOficio).ThenBy(o => o.NumeroOficio).ThenBy(o => o.Id)
            : IncorrectAuditOfficeQueries.Offices(dbContext);
        OficiosConEvidenciaIncorrecta = await incorrectAuditOffices.Take(IncorrectosPreviewLimit).ToListAsync(cancellationToken);
        var incorrectAuditCases = reports.UsesSummary
            ? dbContext.CasosCorreccion.AsNoTracking()
                .Where(c => reports.Cases.Where(r => r.Incorrecto).Select(r => r.Id).Contains(c.Id))
                .Select(c => new IncorrectAuditCaseRow
                {
                    CasoId = c.Id, OficioId = c.OficioId, NumeroOficio = c.Oficio!.NumeroOficio,
                    FechaOficio = c.Oficio.FechaOficio, Consecutivo = c.Consecutivo, Curp = c.Curp,
                    NivelEducativo = c.NivelEducativo, TipoCorreccion = c.TipoCorreccion
                }).OrderByDescending(c => c.FechaOficio).ThenBy(c => c.NumeroOficio).ThenBy(c => c.Consecutivo).ThenBy(c => c.CasoId)
            : IncorrectAuditOfficeQueries.Cases(dbContext);
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
