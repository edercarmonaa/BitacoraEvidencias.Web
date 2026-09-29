namespace BitacoraEvidencias.Web.Services;

public sealed class StagedOficioFolderDeletion
{
    public required string OriginalPath { get; init; }
    public required string StagedPath { get; init; }
}
