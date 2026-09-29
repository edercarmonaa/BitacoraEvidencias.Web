using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages;

public class BusquedaModel(
    AppDbContext dbContext,
    ISensitiveDataProtectionService sensitiveDataProtection) : PageModel
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const string SortByOficio = "oficio";
    private const string SortByFecha = "fecha";
    private const string SortByFechaVerificacion = "fechaVerificacion";
    private const string SortByNivel = "nivel";
    private const string SortByTipo = "tipo";
    private const string SortByEstatus = "estatus";
    private const string SortByCurp = "curp";
    private const string SortByNombre = "nombre";

    [BindProperty(SupportsGet = true)]
    public string? NumeroOficio { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Curp { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Cct { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Folio { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? NivelEducativo { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Estatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool UseContains { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string? SortBy { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SortDirection { get; set; }

    public IReadOnlyList<string> EstatusDisponibles => CatalogosCaso.Estatus;
    public IReadOnlyList<string> NivelesEducativosDisponibles => CatalogosCaso.NivelesEducativos;
    public List<ResultadoRow> Resultados { get; private set; } = [];
    public int TotalRows { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public string ActiveSortBy { get; private set; } = SortByFecha;
    public string ActiveSortDirection { get; private set; } = "desc";
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool IsSortedBy(string sortBy) => string.Equals(ActiveSortBy, sortBy, StringComparison.OrdinalIgnoreCase);
    public bool IsSortDescending => string.Equals(ActiveSortDirection, "desc", StringComparison.OrdinalIgnoreCase);

    public async Task OnGetAsync()
    {
        PageSize = Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);
        ActiveSortBy = NormalizeSortBy(SortBy);
        if (sensitiveDataProtection.IsConfigured && ActiveSortBy is SortByCurp or SortByNombre)
        {
            ActiveSortBy = SortByFecha;
        }
        ActiveSortDirection = NormalizeSortDirection(ActiveSortBy, SortDirection);
        SortBy = ActiveSortBy;
        SortDirection = ActiveSortDirection;
        var likePrefix = UseContains ? "%" : string.Empty;
        var likeSuffix = "%";

        var query = dbContext.CasosCorreccion.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(NumeroOficio))
        {
            var numero = NumeroOficio.Trim();
            var pattern = $"{likePrefix}{numero}{likeSuffix}";
            query = query.Where(x => EF.Functions.Like(x.Oficio!.NumeroOficio, pattern));
        }

        if (!string.IsNullOrWhiteSpace(Curp))
        {
            var curp = Curp.Trim();
            if (sensitiveDataProtection.IsConfigured)
            {
                var curpHash = sensitiveDataProtection.BlindIndex(curp);
                query = query.Where(x => x.CurpHash == curpHash);
            }
            else
            {
                var pattern = $"{likePrefix}{curp}{likeSuffix}";
                query = query.Where(x => EF.Functions.Like(x.Curp ?? string.Empty, pattern));
            }
        }

        if (!string.IsNullOrWhiteSpace(Cct))
        {
            var cct = Cct.Trim();
            if (sensitiveDataProtection.IsConfigured)
            {
                var cctHash = sensitiveDataProtection.BlindIndex(cct);
                query = query.Where(x => x.CctHash == cctHash);
            }
            else
            {
                var pattern = $"{likePrefix}{cct}{likeSuffix}";
                query = query.Where(x => EF.Functions.Like(x.Cct ?? string.Empty, pattern));
            }
        }

        if (!string.IsNullOrWhiteSpace(Folio))
        {
            var folio = Folio.Trim();
            if (sensitiveDataProtection.IsConfigured)
            {
                var folioHash = sensitiveDataProtection.BlindIndex(folio);
                query = query.Where(x => x.FolioHash == folioHash);
            }
            else
            {
                var pattern = $"{likePrefix}{folio}{likeSuffix}";
                query = query.Where(x => EF.Functions.Like(x.Folio ?? string.Empty, pattern));
            }
        }

        if (!string.IsNullOrWhiteSpace(NivelEducativo))
        {
            var nivelEducativo = NivelEducativo.Trim();
            if (CatalogosCaso.EsNivelEducativoValido(nivelEducativo))
            {
                NivelEducativo = nivelEducativo;
                query = query.Where(x => x.NivelEducativo == nivelEducativo);
            }
            else
            {
                NivelEducativo = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(Estatus))
        {
            var estatus = Estatus.Trim();
            if (CatalogosCaso.EsEstatusValido(estatus))
            {
                Estatus = estatus;
                query = query.Where(x => x.Estatus == estatus);
            }
            else
            {
                Estatus = null;
            }
        }

        TotalRows = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRows / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber <= 0 ? 1 : PageNumber, 1, TotalPages);
        var skip = (PageNumber - 1) * PageSize;

        var orderedQuery = ApplyOrdering(query)
            .ThenBy(x => x.Oficio!.NumeroOficio)
            .ThenBy(x => x.NivelEducativo)
            .ThenBy(x => x.Consecutivo)
            .ThenBy(x => x.Id);

        Resultados = await orderedQuery
            .Skip(skip)
            .Take(PageSize)
            .Select(x => new ResultadoRow
            {
                CasoId = x.Id,
                NumeroOficio = x.Oficio!.NumeroOficio,
                FechaOficio = x.Oficio.FechaOficio,
                FechaVerificacion = x.FechaVerificacion,
                Consecutivo = x.Consecutivo,
                Curp = x.Curp,
                NombreCompleto = x.NombreCompleto,
                NivelEducativo = x.NivelEducativo,
                TipoCorreccion = x.TipoCorreccion,
                Estatus = x.Estatus
            })
            .ToListAsync();
    }

    public string GetSortIndicator(string sortBy)
    {
        if (!IsSortedBy(sortBy))
        {
            return string.Empty;
        }

        return IsSortDescending ? "Desc" : "Asc";
    }

    public string GetNextSortDirection(string sortBy)
    {
        if (!IsSortedBy(sortBy))
        {
            return GetDefaultSortDirection(sortBy);
        }

        return IsSortDescending ? "asc" : "desc";
    }

    public static string GetEstatusBadgeClass(string estatus) => estatus switch
    {
        "Pendiente" => "text-bg-warning",
        "En proceso" => "text-bg-info",
        "Completado" => "text-bg-success",
        "Rechazado" => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string GetNivelBadgeClass(string nivelEducativo) => nivelEducativo switch
    {
        "Primaria" => "search-badge search-badge--primary",
        "Secundaria" => "search-badge search-badge--secondary",
        _ => "search-badge search-badge--neutral"
    };

    private IOrderedQueryable<CasoCorreccion> ApplyOrdering(IQueryable<CasoCorreccion> query) => (ActiveSortBy, ActiveSortDirection) switch
    {
        (SortByOficio, "desc") => query.OrderByDescending(x => x.Oficio!.NumeroOficio),
        (SortByOficio, _) => query.OrderBy(x => x.Oficio!.NumeroOficio),
        (SortByFecha, "asc") => query.OrderBy(x => x.Oficio!.FechaOficio),
        (SortByFecha, _) => query.OrderByDescending(x => x.Oficio!.FechaOficio),
        (SortByFechaVerificacion, "asc") => query.OrderBy(x => x.FechaVerificacion),
        (SortByFechaVerificacion, _) => query.OrderByDescending(x => x.FechaVerificacion),
        (SortByNivel, "desc") => query.OrderByDescending(x => x.NivelEducativo),
        (SortByNivel, _) => query.OrderBy(x => x.NivelEducativo),
        (SortByTipo, "desc") => query.OrderByDescending(x => x.TipoCorreccion),
        (SortByTipo, _) => query.OrderBy(x => x.TipoCorreccion),
        (SortByEstatus, "desc") => query.OrderByDescending(x => x.Estatus),
        (SortByEstatus, _) => query.OrderBy(x => x.Estatus),
        (SortByCurp, "desc") => query.OrderByDescending(x => x.Curp ?? string.Empty),
        (SortByCurp, _) => query.OrderBy(x => x.Curp ?? string.Empty),
        (SortByNombre, "desc") => query.OrderByDescending(x => x.NombreCompleto ?? string.Empty),
        (SortByNombre, _) => query.OrderBy(x => x.NombreCompleto ?? string.Empty),
        _ => query.OrderByDescending(x => x.Oficio!.FechaOficio)
    };

    private static string NormalizeSortBy(string? sortBy) => sortBy switch
    {
        SortByOficio => SortByOficio,
        SortByFecha => SortByFecha,
        SortByFechaVerificacion => SortByFechaVerificacion,
        SortByNivel => SortByNivel,
        SortByTipo => SortByTipo,
        SortByEstatus => SortByEstatus,
        SortByCurp => SortByCurp,
        SortByNombre => SortByNombre,
        _ => SortByFecha
    };

    private static string NormalizeSortDirection(string sortBy, string? sortDirection)
    {
        if (string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        if (string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            return "desc";
        }

        return GetDefaultSortDirection(sortBy);
    }

    private static string GetDefaultSortDirection(string sortBy) => sortBy switch
    {
        SortByFecha => "desc",
        SortByFechaVerificacion => "desc",
        _ => "asc"
    };

    public class ResultadoRow
    {
        public int CasoId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public DateTime FechaOficio { get; init; }
        public DateTime? FechaVerificacion { get; init; }
        public int Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string? NombreCompleto { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string TipoCorreccion { get; init; } = string.Empty;
        public string Estatus { get; init; } = string.Empty;
        public string ConsecutivoTexto => Consecutivo.ToString("000");
    }
}
