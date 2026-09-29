using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Oficios;

public class IndexModel(AppDbContext dbContext) : PageModel
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [TempData]
    public string? FlashSuccess { get; set; }

    [TempData]
    public string? FlashError { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    public List<OficioRow> Oficios { get; private set; } = [];
    public int TotalRows { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public int MaxAllowedPageSize => MaxPageSize;

    public async Task OnGetAsync()
    {
        PageSize = Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);
        var query = dbContext.Oficios.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim();
            query = query.Where(x =>
                EF.Functions.Like(x.NumeroOficio, $"%{term}%") ||
                EF.Functions.Like(x.Asunto, $"%{term}%"));
        }

        TotalRows = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRows / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber <= 0 ? 1 : PageNumber, 1, TotalPages);
        var skip = (PageNumber - 1) * PageSize;

        Oficios = await query
            .OrderByDescending(x => x.FechaOficio)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(PageSize)
            .Select(x => new OficioRow
            {
                Id = x.Id,
                NumeroOficio = x.NumeroOficio,
                FechaOficio = x.FechaOficio,
                Asunto = x.Asunto,
                TotalCasos = x.Casos.Count,
                TotalEvidencias = x.Casos.SelectMany(c => c.Evidencias).Count(),
                CasosSinFuenteEvidencia = x.Casos.Count(c => !c.Evidencias.Any() && (c.EvidenciaUrl == null || c.EvidenciaUrl == string.Empty)),
                CasosPendientesAuditoria = x.Casos.Count(c => c.AuditoriaEstado == CatalogosAuditoriaCaso.Pendiente),
                CasosIncorrectosAuditoria = x.Casos.Count(c => c.AuditoriaEstado == CatalogosAuditoriaCaso.NoCoincidente)
            })
            .ToListAsync();
    }

    public class OficioRow
    {
        public int Id { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public DateTime FechaOficio { get; init; }
        public string Asunto { get; init; } = string.Empty;
        public int TotalCasos { get; init; }
        public int TotalEvidencias { get; init; }
        public int CasosSinFuenteEvidencia { get; init; }
        public int CasosPendientesAuditoria { get; init; }
        public int CasosIncorrectosAuditoria { get; init; }
        public string AuditoriaEstado => ResolveAuditoriaEstado();
        public string AuditoriaDescripcion => AuditoriaEstado switch
        {
            "ok" => "Auditado correcto",
            "incorrecto" => "Auditado incorrecto",
            _ => "Pendiente de auditoria"
        };

        private string ResolveAuditoriaEstado()
        {
            if (TotalCasos == 0 || CasosSinFuenteEvidencia > 0 || CasosPendientesAuditoria > 0)
            {
                return "pendiente";
            }

            if (CasosIncorrectosAuditoria > 0)
            {
                return "incorrecto";
            }

            return "ok";
        }
    }
}
