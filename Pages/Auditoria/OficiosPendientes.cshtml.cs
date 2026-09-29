using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Auditoria;

public class OficiosPendientesModel(AppDbContext dbContext) : PageModel
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    [BindProperty(SupportsGet = true, Name = "numeroOficio")]
    public string NumeroOficioFiltro { get; set; } = string.Empty;

    public List<OficioPendienteRow> Items { get; private set; } = [];
    public int TotalRows { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public int TotalOficiosAuditables { get; private set; }
    public int MaxAllowedPageSize => MaxPageSize;
    public bool HasNumeroOficioFiltro => !string.IsNullOrWhiteSpace(NumeroOficioFiltro);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PageSize = Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);
        NumeroOficioFiltro = NumeroOficioFiltro?.Trim() ?? string.Empty;

        var query = dbContext.Oficios
            .AsNoTracking()
            .Where(x =>
                x.Casos.Any() &&
                x.Casos.All(c => c.Evidencias.Any() || !string.IsNullOrEmpty(c.EvidenciaUrl)) &&
                x.Casos.Any(c => string.IsNullOrEmpty(c.AuditoriaEstado) || c.AuditoriaEstado == CatalogosAuditoriaCaso.Pendiente));

        TotalOficiosAuditables = await query.CountAsync(cancellationToken);

        if (HasNumeroOficioFiltro)
        {
            query = query.Where(x => EF.Functions.Like(x.NumeroOficio, $"%{NumeroOficioFiltro}%"));
        }

        TotalRows = await query.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRows / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber <= 0 ? 1 : PageNumber, 1, TotalPages);
        var skip = (PageNumber - 1) * PageSize;

        Items = await query
            .OrderByDescending(x => x.FechaOficio)
            .ThenBy(x => x.NumeroOficio)
            .Skip(skip)
            .Take(PageSize)
            .Select(x => new OficioPendienteRow
            {
                Id = x.Id,
                NumeroOficio = x.NumeroOficio,
                FechaOficio = x.FechaOficio,
                Asunto = x.Asunto,
                TotalCasos = x.Casos.Count,
                TotalFuentesEvidencia = x.Casos.Sum(c =>
                    c.Evidencias.Count() + (!string.IsNullOrEmpty(c.EvidenciaUrl) ? 1 : 0)),
                FirstPendingCaseId = x.Casos
                    .Where(c => string.IsNullOrEmpty(c.AuditoriaEstado) || c.AuditoriaEstado == CatalogosAuditoriaCaso.Pendiente)
                    .OrderBy(c => c.Consecutivo)
                    .Select(c => c.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
    }

    public sealed class OficioPendienteRow
    {
        public int Id { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public DateTime FechaOficio { get; init; }
        public string Asunto { get; init; } = string.Empty;
        public int TotalCasos { get; init; }
        public int TotalFuentesEvidencia { get; init; }
        public int FirstPendingCaseId { get; init; }
    }
}
