namespace BitacoraEvidencias.Web.Pages.Shared;

public class PaginationViewModel
{
    public int PageNumber { get; init; }
    public int TotalPages { get; init; }
    public int PageSize { get; init; }
    public int TotalRows { get; init; }
    public int CurrentCount { get; init; }
    public string PageName { get; init; } = string.Empty;
    public string RowsLabel { get; init; } = "registro(s)";
    public string? MaxPageSizeText { get; init; }
    public Dictionary<string, string> RouteValues { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public bool UseHtmx { get; init; }
    public string HxTarget { get; init; } = string.Empty;
    public string HxSelect { get; init; } = string.Empty;
    public string HxSwap { get; init; } = "outerHTML transition:true";
    public bool HxPushUrl { get; init; } = true;

    public int FirstRow => TotalRows == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int LastRow => TotalRows == 0 ? 0 : ((PageNumber - 1) * PageSize) + CurrentCount;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
