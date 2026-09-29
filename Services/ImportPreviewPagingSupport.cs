namespace BitacoraEvidencias.Web.Services;

public static class ImportPreviewPagingSupport
{
    public static int NormalizePageSize(int pageSize, int defaultPageSize, int maxPageSize)
        => Math.Clamp(pageSize <= 0 ? defaultPageSize : pageSize, 1, maxPageSize);

    public static (int PageNumber, int TotalPages, IReadOnlyList<T> Rows) ApplyPaging<T>(
        IReadOnlyList<T> rows,
        int pageNumber,
        int pageSize,
        Func<T, int> rowNumberSelector)
    {
        var totalRows = rows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)pageSize));
        var normalizedPageNumber = Math.Clamp(pageNumber <= 0 ? 1 : pageNumber, 1, totalPages);
        var skip = (normalizedPageNumber - 1) * pageSize;

        var pageRows = rows
            .OrderBy(rowNumberSelector)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return (normalizedPageNumber, totalPages, pageRows);
    }
}
