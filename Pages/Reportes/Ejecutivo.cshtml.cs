using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Services;
using BitacoraEvidencias.Web.ViewComponents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Reportes;

public class EjecutivoModel(AppDbContext dbContext, ReportReadService? reports = null) : PageModel
{
    public DateTime? SummaryUpdatedAtUtc { get; private set; }
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime? FechaFin { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    public int TotalOficiosVerificados { get; private set; }
    public int TotalCasosVerificados { get; private set; }
    public int TotalEvidencias { get; private set; }
    public int TotalCasosPendientes { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public int MaxAllowedPageSize => MaxPageSize;

    public IReadOnlyList<DashboardStatCardModel> StatCards { get; private set; } = [];
    public List<ResumenMensualRow> ResumenMensual { get; private set; } = [];
    public List<OficioPorAnioRow> OficiosPorAnioFechaOficio { get; private set; } = [];
    public List<NivelEducativoRow> CasosPorNivel { get; private set; } = [];
    public List<TipoCorreccionRow> OficiosPorTipoCorreccion { get; private set; } = [];
    public List<CasoPendienteRow> CasosPendientes { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        NormalizeFilters();
        PageSize = Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);

        var inicio = FechaInicio!.Value.Date;
        var finExclusivo = FechaFin!.Value.Date.AddDays(1);
        reports ??= new ReportReadService(dbContext, new ConfigurationBuilder().Build());
        await reports.InitializeAsync(cancellationToken);
        SummaryUpdatedAtUtc = reports.UpdatedAtUtc;
        var casosVerificados = reports.Cases
            .Where(x => x.FechaVerificacion.HasValue &&
                        x.FechaVerificacion.Value >= inicio &&
                        x.FechaVerificacion.Value < finExclusivo);

        TotalCasosVerificados = await casosVerificados.CountAsync(cancellationToken);
        TotalOficiosVerificados = await reports.Offices
            .Where(x => x.PrimeraFechaVerificacion >= inicio &&
                        x.PrimeraFechaVerificacion < finExclusivo)
            .CountAsync(cancellationToken);

        var casosPorMes = await casosVerificados
            .GroupBy(x => new
            {
                x.FechaVerificacion!.Value.Year,
                x.FechaVerificacion!.Value.Month
            })
            .Select(group => new MonthTotalQueryRow
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                Total = group.Count()
            })
            .ToListAsync(cancellationToken);

        var oficiosPorMes = await reports.Offices
            .Where(x => x.PrimeraFechaVerificacion >= inicio &&
                        x.PrimeraFechaVerificacion < finExclusivo)
            .GroupBy(x => new
            {
                x.PrimeraFechaVerificacion!.Value.Year,
                x.PrimeraFechaVerificacion!.Value.Month
            })
            .Select(group => new MonthTotalQueryRow
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                Total = group.Count()
            })
            .ToListAsync(cancellationToken);

        ResumenMensual = BuildResumenMensual(inicio, finExclusivo, casosPorMes, oficiosPorMes);

        OficiosPorAnioFechaOficio = await reports.Offices
            .Where(x => x.PrimeraFechaVerificacion >= inicio &&
                        x.PrimeraFechaVerificacion < finExclusivo)
            .GroupBy(x => x.FechaOficio.Year)
            .Select(group => new OficioPorAnioRow
            {
                Anio = group.Key,
                TotalOficios = group.Count()
            })
            .OrderBy(x => x.Anio)
            .ToListAsync(cancellationToken);

        var totalEvidenciasFoto = await casosVerificados.SumAsync(x => x.Fotos, cancellationToken);
        var totalEvidenciasUrl = await casosVerificados
            .CountAsync(x => x.TieneUrl, cancellationToken);
        TotalEvidencias = totalEvidenciasFoto + totalEvidenciasUrl;

        CasosPorNivel = await casosVerificados
            .GroupBy(x => x.NivelEducativo)
            .Select(group => new NivelEducativoRow
            {
                NivelEducativo = group.Key,
                Total = group.Count()
            })
            .OrderBy(x => x.NivelEducativo)
            .ToListAsync(cancellationToken);

        OficiosPorTipoCorreccion = await casosVerificados
            .Select(x => new { x.TipoCorreccion, NumeroOficio = x.NumeroOficio.Trim() }).Distinct()
            .GroupBy(x => x.TipoCorreccion)
            .Select(group => new TipoCorreccionRow
            {
                TipoCorreccion = group.Key,
                TotalOficios = group.Count()
            })
            .OrderByDescending(x => x.TotalOficios)
            .ThenBy(x => x.TipoCorreccion)
            .ToListAsync(cancellationToken);

        var pendientesQuery = casosVerificados
            .Where(x => x.Estatus != CatalogosCaso.EstatusCompletado);

        TotalCasosPendientes = await pendientesQuery.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCasosPendientes / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber <= 0 ? 1 : PageNumber, 1, TotalPages);
        var skip = (PageNumber - 1) * PageSize;

        CasosPendientes = await (from x in pendientesQuery
                                join actual in dbContext.CasosCorreccion.AsNoTracking() on x.Id equals actual.Id
                                select new { Summary = x, actual.Curp })
            .OrderByDescending(x => x.Summary.FechaVerificacion)
            .ThenBy(x => x.Summary.NumeroOficio)
            .ThenBy(x => x.Summary.Consecutivo)
            .ThenBy(x => x.Summary.Id)
            .Skip(skip)
            .Take(PageSize)
            .Select(x => new CasoPendienteRow
            {
                CasoId = x.Summary.Id,
                NumeroOficio = x.Summary.NumeroOficio,
                Consecutivo = x.Summary.Consecutivo,
                Curp = x.Curp,
                NivelEducativo = x.Summary.NivelEducativo,
                TipoCorreccion = x.Summary.TipoCorreccion,
                Estatus = x.Summary.Estatus,
                FechaVerificacion = x.Summary.FechaVerificacion
            })
            .ToListAsync(cancellationToken);

        StatCards =
        [
            new DashboardStatCardModel
            {
                Title = "Oficios verificados",
                Value = TotalOficiosVerificados,
                AccentClass = "border-start border-4 border-brand-guinda"
            },
            new DashboardStatCardModel
            {
                Title = "Casos verificados",
                Value = TotalCasosVerificados,
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
                Title = "Casos pendientes",
                Value = TotalCasosPendientes,
                AccentClass = "border-start border-4 border-warning"
            }
        ];
    }

    private void NormalizeFilters()
    {
        var today = DateTime.Today;
        FechaInicio ??= new DateTime(today.Year, today.Month, 1);
        FechaFin ??= today;

        FechaInicio = FechaInicio.Value.Date;
        FechaFin = FechaFin.Value.Date;

        if (FechaFin < FechaInicio)
        {
            FechaFin = FechaInicio;
        }
    }

    private static List<ResumenMensualRow> BuildResumenMensual(
        DateTime inicio,
        DateTime finExclusivo,
        IEnumerable<MonthTotalQueryRow> casosPorMes,
        IEnumerable<MonthTotalQueryRow> oficiosPorMes)
    {
        var casosLookup = casosPorMes.ToDictionary(
            x => MonthKey(x.Year, x.Month),
            x => x.Total);
        var oficiosLookup = oficiosPorMes.ToDictionary(
            x => MonthKey(x.Year, x.Month),
            x => x.Total);

        var result = new List<ResumenMensualRow>();
        var month = new DateTime(inicio.Year, inicio.Month, 1);
        var lastMonth = new DateTime(finExclusivo.AddDays(-1).Year, finExclusivo.AddDays(-1).Month, 1);
        var culture = CultureInfo.GetCultureInfo("es-MX");

        while (month <= lastMonth)
        {
            var key = MonthKey(month.Year, month.Month);
            result.Add(new ResumenMensualRow
            {
                Anio = month.Year,
                Mes = month.Month,
                Periodo = culture.TextInfo.ToTitleCase(month.ToString("MMMM yyyy", culture)),
                TotalOficios = oficiosLookup.GetValueOrDefault(key),
                TotalCasos = casosLookup.GetValueOrDefault(key)
            });

            month = month.AddMonths(1);
        }

        return result;
    }

    private static int MonthKey(int year, int month)
    {
        return (year * 100) + month;
    }

    public class ResumenMensualRow
    {
        public int Anio { get; init; }
        public int Mes { get; init; }
        public string Periodo { get; init; } = string.Empty;
        public int TotalOficios { get; init; }
        public int TotalCasos { get; init; }
    }

    public class OficioPorAnioRow
    {
        public int Anio { get; init; }
        public int TotalOficios { get; init; }
    }

    private sealed class MonthTotalQueryRow
    {
        public int Year { get; init; }
        public int Month { get; init; }
        public int Total { get; init; }
    }

    public class NivelEducativoRow
    {
        public string NivelEducativo { get; init; } = string.Empty;
        public int Total { get; init; }
    }

    public class TipoCorreccionRow
    {
        public string TipoCorreccion { get; init; } = string.Empty;
        public int TotalOficios { get; init; }
    }

    public class CasoPendienteRow
    {
        public int CasoId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public string Estatus { get; init; } = string.Empty;
        public DateTime? FechaVerificacion { get; init; }
        public string ConsecutivoTexto => Consecutivo.ToString("000");
    }
}
