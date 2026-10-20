using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Reportes;

public class OficiosPorTipoCorreccionModel(AppDbContext dbContext, ReportReadService? reports = null) : PageModel
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    public List<TipoCorreccionRow> Items { get; private set; } = [];
    public int TotalRows { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public int MaxAllowedPageSize => MaxPageSize;

    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        PageSize = Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);

        reports ??= new ReportReadService(dbContext, new ConfigurationBuilder().Build());
        await reports.InitializeAsync(cancellationToken);
        var query = reports.Cases
            .Where(x => x.Estatus == "Completado")
            .Select(x => new { x.TipoCorreccion, x.OficioId }).Distinct()
            .GroupBy(x => x.TipoCorreccion)
            .Select(group => new TipoCorreccionRow
            {
                TipoCorreccion = group.Key,
                Total = group.Count()
            });

        TotalRows = await query.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRows / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber <= 0 ? 1 : PageNumber, 1, TotalPages);
        var skip = (PageNumber - 1) * PageSize;

        Items = await query
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.TipoCorreccion)
            .Skip(skip)
            .Take(PageSize)
            .ToListAsync(cancellationToken);
    }

    public class TipoCorreccionRow
    {
        public string TipoCorreccion { get; init; } = string.Empty;
        public int Total { get; init; }
    }
}
