namespace BitacoraEvidencias.Web.Services;

public class TabularImportFile
{
    public string FileName { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public int TotalRows { get; set; }
    public List<string> Headers { get; } = [];
    public List<TabularImportRow> Rows { get; } = [];
    public List<string> Errors { get; } = [];
}

public class TabularImportRow
{
    public int SourceRowNumber { get; init; }
    public List<string> Values { get; } = [];
}
