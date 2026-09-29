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

    public async Task OnGetAsync()
    {
        TotalOficios = await dbContext.Oficios.CountAsync();
        TotalCasos = await dbContext.CasosCorreccion.CountAsync();
        TotalEvidencias = await dbContext.EvidenciasFoto.CountAsync();
        TotalPendientesEvidencia = await dbContext.CasosCorreccion
            .AsNoTracking()
            .CountAsync(x => !x.Evidencias.Any() && (x.EvidenciaUrl == null || x.EvidenciaUrl == string.Empty));

        PendientesEvidencia = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => !x.Evidencias.Any() && (x.EvidenciaUrl == null || x.EvidenciaUrl == string.Empty))
            .OrderByDescending(x => x.CreadoEn)
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
            .ToListAsync();

        var oficiosPorTipoCorreccionQuery = dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.Estatus == "Completado")
            .GroupBy(x => x.TipoCorreccion)
            .Select(group => new TipoCorreccionRow
            {
                TipoCorreccion = group.Key,
                Total = group.Select(x => x.OficioId).Distinct().Count()
            });

        TotalTiposCorreccionCompletados = await oficiosPorTipoCorreccionQuery.CountAsync();
        CasosPorTipoCorreccion = await oficiosPorTipoCorreccionQuery
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.TipoCorreccion)
            .Take(TiposCorreccionPreviewLimit)
            .ToListAsync();

        var casosNoCompletadosQuery = dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.Estatus != "Completado");

        TotalCasosNoCompletados = await casosNoCompletadosQuery.CountAsync();
        CasosNoCompletados = await casosNoCompletadosQuery
            .OrderByDescending(x => x.CreadoEn)
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
            .ToListAsync();

        var auditableOficios = await dbContext.Oficios
            .AsNoTracking()
            .Where(x =>
                x.Casos.Any() &&
                x.Casos.All(c => c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl != string.Empty)))
            .Select(x => new
            {
                Estados = x.Casos.Select(c => c.AuditoriaEstado).ToList()
            })
            .ToListAsync();

        TotalOficiosAuditadosCorrectos = auditableOficios.Count(x =>
            x.Estados.Count > 0 &&
            x.Estados.All(estado => CatalogosAuditoriaCaso.NormalizarEstado(estado) == CatalogosAuditoriaCaso.Coincidente));

        var incorrectAuditOffices = await IncorrectAuditOfficeQueries.LoadAsync(dbContext);
        TotalOficiosConEvidenciaIncorrecta = incorrectAuditOffices.Count;
        OficiosConEvidenciaIncorrecta = incorrectAuditOffices
            .Take(IncorrectosPreviewLimit)
            .ToList();
        var incorrectAuditCases = await IncorrectAuditOfficeQueries.LoadCasesAsync(dbContext);
        TotalCasosConEvidenciaIncorrecta = incorrectAuditCases.Count;
        CasosConEvidenciaIncorrecta = incorrectAuditCases
            .Take(IncorrectosPreviewLimit)
            .ToList();

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
