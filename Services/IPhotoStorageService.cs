namespace BitacoraEvidencias.Web.Services;

public interface IPhotoStorageService
{
    Task<StoredPhotoInfo> SaveEvidenceAsync(IFormFile file, string numeroOficio, int consecutivo, CancellationToken cancellationToken = default);
    Task<StoredPhotoInfo> SaveEvidenceFromPathAsync(string sourceFilePath, string numeroOficio, int consecutivo, CancellationToken cancellationToken = default);
    bool TryValidateSourceEvidenceFile(string sourceFilePath, out string? errorMessage);
    bool TryOpenReadEvidence(string relativePath, out Stream? stream, out string contentType);
    bool TryProtectExistingEvidenceFile(string relativePath, out string? errorMessage);
    bool TryProtectExistingOficioFolder(string numeroOficio, out string? errorMessage);
    void DeleteEvidenceFiles(string? relativeFilePath, string? relativeThumbnailPath);
    void DeleteCaseFolder(string numeroOficio, int consecutivo);
    void DeleteOficioFolder(string numeroOficio);
    bool TryStageDeleteEvidenceFiles(string? relativeFilePath, string? relativeThumbnailPath, out StagedEvidenceDeletion? mutation, out string? errorMessage);
    bool TryRollbackStagedEvidenceDeletion(StagedEvidenceDeletion? mutation, out string? errorMessage);
    bool TryFinalizeStagedEvidenceDeletion(StagedEvidenceDeletion? mutation, out string? errorMessage);
    bool TryStageDeleteOficioFolder(string numeroOficio, out StagedOficioFolderDeletion? mutation, out string? errorMessage);
    bool TryRollbackStagedOficioFolderDeletion(StagedOficioFolderDeletion? mutation, out string? errorMessage);
    bool TryFinalizeStagedOficioFolderDeletion(StagedOficioFolderDeletion? mutation, out string? errorMessage);
    bool TryStageDeleteAndReorderCaseFolders(string numeroOficio, int deletedConsecutivo, IReadOnlyCollection<(int OldConsecutivo, int NewConsecutivo)> moves, out StagedCaseFolderMutation? mutation, out string? errorMessage);
    bool TryRollbackStagedCaseFolderMutation(StagedCaseFolderMutation? mutation, out string? errorMessage);
    bool TryFinalizeStagedCaseFolderMutation(StagedCaseFolderMutation? mutation, out string? errorMessage);
    bool TryRenameOficioFolder(string oldNumeroOficio, string newNumeroOficio, out string? errorMessage);
    bool TryReorderCaseFolders(string numeroOficio, IReadOnlyCollection<(int OldConsecutivo, int NewConsecutivo)> moves, out string? errorMessage);
    string RebaseRelativePathForCase(string relativePath, string numeroOficio, int oldConsecutivo, int newConsecutivo);
    string RebaseRelativePathForOficio(string relativePath, string oldNumeroOficio, string newNumeroOficio);
}
