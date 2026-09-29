namespace BitacoraEvidencias.Web.Services;

public sealed class StagedEvidenceDeletion
{
    public List<StagedEvidenceFile> Files { get; } = [];
}

public sealed class StagedEvidenceFile
{
    public required string OriginalPath { get; init; }
    public required string StagedPath { get; init; }
}
