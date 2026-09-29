namespace BitacoraEvidencias.Web.Services;

public sealed class StagedCaseFolderMutation
{
    public string NumeroOficio { get; init; } = string.Empty;
    public string? OriginalDeletedCasePath { get; set; }
    public string? StagedDeletedCasePath { get; set; }
    public List<StagedCaseFolderMove> ReorderedFolders { get; } = [];
}

public sealed class StagedCaseFolderMove
{
    public string OriginalPath { get; init; } = string.Empty;
    public string TempPath { get; init; } = string.Empty;
    public string NewPath { get; init; } = string.Empty;
    public string CurrentPath { get; set; } = string.Empty;
}
