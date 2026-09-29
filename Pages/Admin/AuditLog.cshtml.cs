using BitacoraEvidencias.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class AuditLogModel(AppDbContext dbContext) : PageModel
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [BindProperty(SupportsGet = true)]
    public string? EventType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Username { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Days { get; set; } = 7;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = DefaultPageSize;

    public int MaxAllowedPageSize => MaxPageSize;
    public List<AuditLogRow> Items { get; private set; } = [];
    public int TotalRows { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public async Task OnGetAsync()
    {
        var effectiveDays = Math.Clamp(Days <= 0 ? 7 : Days, 1, 90);
        Days = effectiveDays;
        PageSize = Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);
        PageNumber = PageNumber <= 0 ? 1 : PageNumber;

        var eventTypeFilter = string.IsNullOrWhiteSpace(EventType)
            ? null
            : EventType.Trim().ToUpperInvariant();

        var usernameFilter = string.IsNullOrWhiteSpace(Username)
            ? null
            : Username.Trim();

        var fromUtc = DateTime.UtcNow.AddDays(-effectiveDays);
        var query = dbContext.AuditLogs
            .AsNoTracking()
            .Where(x => x.OccurredAtUtc >= fromUtc);

        if (!string.IsNullOrWhiteSpace(eventTypeFilter))
        {
            query = query.Where(x => x.EventType == eventTypeFilter);
        }

        if (!string.IsNullOrWhiteSpace(usernameFilter))
        {
            query = query.Where(x => x.Username != null && EF.Functions.Like(x.Username, $"%{usernameFilter}%"));
        }

        TotalRows = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalRows / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        var skip = (PageNumber - 1) * PageSize;

        Items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip(skip)
            .Take(PageSize)
            .Select(x => new AuditLogRow
            {
                Id = x.Id,
                OccurredAtUtc = x.OccurredAtUtc,
                EventType = x.EventType,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                Username = x.Username,
                Role = x.Role,
                IpAddress = x.IpAddress,
                NumeroOficio = x.NumeroOficio,
                Consecutivo = x.Consecutivo,
                Curp = x.Curp ?? (x.CurpSuffix == null ? null : $"***{x.CurpSuffix}"),
                Cct = x.Cct ?? (x.CctSuffix == null ? null : $"***{x.CctSuffix}"),
                FolioCertificado = x.FolioCertificado ?? (x.FolioCertificadoSuffix == null ? null : $"***{x.FolioCertificadoSuffix}"),
                EvidenciasCount = x.EvidenciasCount,
                Details = x.Details,
                Success = x.Success
            })
            .ToListAsync();

    }

    public class AuditLogRow
    {
        public long Id { get; init; }
        public DateTime OccurredAtUtc { get; init; }
        public string EventType { get; init; } = string.Empty;
        public string? EntityType { get; init; }
        public int? EntityId { get; init; }
        public string? Username { get; init; }
        public string? Role { get; init; }
        public string? IpAddress { get; init; }
        public string? NumeroOficio { get; init; }
        public int? Consecutivo { get; init; }
        public string? Curp { get; init; }
        public string? Cct { get; init; }
        public string? FolioCertificado { get; init; }
        public int? EvidenciasCount { get; init; }
        public string? Details { get; init; }
        public bool Success { get; init; }

        public DateTime OccurredAtLocal => DateTime.SpecifyKind(OccurredAtUtc, DateTimeKind.Utc).ToLocalTime();
    }
}
