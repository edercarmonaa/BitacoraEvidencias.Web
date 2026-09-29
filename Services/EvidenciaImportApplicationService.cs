using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Admin;
using BitacoraEvidencias.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface IEvidenciaImportApplicationService
{
    Task<EvidenciaImportExecutionResult> ImportAsync(
        ImportEvidenciasModel.EvidenciaImportPreview preview,
        CancellationToken cancellationToken = default);
}

public sealed class EvidenciaImportExecutionResult : ApplicationCommandResult
{
    public int ImportedCount { get; init; }
    public IReadOnlyList<ImportEvidenciasModel.EvidenciaImportRow> FailedRows { get; init; } = [];
}

public sealed class EvidenciaImportApplicationService(
    AppDbContext dbContext,
    IPhotoStorageService photoStorageService,
    IAuditLogService auditLogService,
    ICaseAuditService caseAuditService) : IEvidenciaImportApplicationService
{
    public async Task<EvidenciaImportExecutionResult> ImportAsync(
        ImportEvidenciasModel.EvidenciaImportPreview preview,
        CancellationToken cancellationToken = default)
    {
        if (!preview.CanImport)
        {
            return new EvidenciaImportExecutionResult
            {
                ErrorMessage = "La vista previa no contiene registros validos para importar."
            };
        }

        var caseTargets = await LoadCaseTargetsAsync(cancellationToken);
        var importedCount = 0;
        var failedRows = new List<ImportEvidenciasModel.EvidenciaImportRow>();

        foreach (var row in preview.Rows.OrderBy(item => item.SourceRowNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryResolveCaseTarget(caseTargets, row.NumeroOficio, row.NivelEducativo, out var caseTarget, out _))
            {
                failedRows.Add(row);
                continue;
            }

            StoredPhotoInfo storedPhoto;
            try
            {
                storedPhoto = await photoStorageService.SaveEvidenceFromPathAsync(
                    row.SourceFilePath,
                    row.NumeroOficio,
                    caseTarget!.Consecutivo,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                failedRows.Add(row);
                continue;
            }

            CasoCorreccion? auditCase = null;
            var auditResetApplied = false;
            if (CatalogosAuditoriaCaso.NormalizarEstado(caseTarget.AuditoriaEstado) != CatalogosAuditoriaCaso.Pendiente)
            {
                auditCase = await dbContext.CasosCorreccion
                    .Include(x => x.Oficio)
                    .FirstOrDefaultAsync(x => x.Id == caseTarget.CasoCorreccionId, cancellationToken);

                if (auditCase is null)
                {
                    failedRows.Add(row);
                    continue;
                }

                auditResetApplied = caseAuditService.ResetToPending(auditCase);
            }

            dbContext.EvidenciasFoto.Add(new EvidenciaFoto
            {
                CasoCorreccionId = caseTarget.CasoCorreccionId,
                TipoEvidencia = row.TipoEvidencia,
                FechaEvidencia = row.FechaEvidencia,
                Notas = row.Notas,
                RutaArchivo = storedPhoto.RelativeFilePath,
                RutaMiniatura = storedPhoto.RelativeThumbnailPath
            });

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                importedCount++;

                if (auditResetApplied && auditCase is not null)
                {
                    await caseAuditService.WriteResetLogAsync(
                        auditCase,
                        "La auditoria regreso a pendiente por importacion de evidencia.",
                        cancellationToken);
                    caseTarget.AuditoriaEstado = CatalogosAuditoriaCaso.Pendiente;
                }
            }
            catch (DbUpdateException)
            {
                photoStorageService.DeleteEvidenceFiles(storedPhoto.RelativeFilePath, storedPhoto.RelativeThumbnailPath);
                failedRows.Add(row);
            }
            finally
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Import,
            EntityType = "ImportacionEvidencias",
            EvidenciasCount = importedCount,
            Details = failedRows.Count > 0
                ? $"Importacion masiva parcial de {importedCount} evidencia(s) desde {preview.FileName}. Filas pendientes: {failedRows.Count}."
                : $"Importacion masiva de {importedCount} evidencia(s) desde {preview.FileName}.",
            Success = failedRows.Count == 0 || importedCount > 0
        }, cancellationToken);

        return new EvidenciaImportExecutionResult
        {
            ImportedCount = importedCount,
            FailedRows = failedRows
        };
    }

    private async Task<Dictionary<string, List<CaseImportTarget>>> LoadCaseTargetsAsync(CancellationToken cancellationToken)
    {
        var cases = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.Oficio != null)
            .Select(x => new CaseImportTarget
            {
                OficioId = x.OficioId,
                CasoCorreccionId = x.Id,
                NumeroOficio = x.Oficio!.NumeroOficio,
                Consecutivo = x.Consecutivo,
                NivelEducativo = x.NivelEducativo,
                AuditoriaEstado = x.AuditoriaEstado
            })
            .ToListAsync(cancellationToken);

        return cases
            .GroupBy(item => NormalizeLookupKey(item.NumeroOficio), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.Consecutivo).ToList(),
                StringComparer.Ordinal);
    }

    private static bool TryResolveCaseTarget(
        IReadOnlyDictionary<string, List<CaseImportTarget>> caseTargets,
        string numeroOficio,
        string nivelEducativo,
        out CaseImportTarget? caseTarget,
        out string? errorMessage)
    {
        var normalizedNumeroOficio = NormalizeLookupKey(numeroOficio);
        if (string.IsNullOrWhiteSpace(normalizedNumeroOficio) ||
            !caseTargets.TryGetValue(normalizedNumeroOficio, out var officeCases))
        {
            caseTarget = null;
            errorMessage = "OFICIO no existe en la base.";
            return false;
        }

        var exactOfficeCases = officeCases
            .Where(item => string.Equals(item.NumeroOficio.Trim(), numeroOficio.Trim(), StringComparison.Ordinal))
            .ToList();

        var candidateOfficeCases = exactOfficeCases.Count > 0
            ? exactOfficeCases
            : officeCases;

        if (exactOfficeCases.Count == 0 &&
            candidateOfficeCases
                .Select(item => item.OficioId)
                .Distinct()
                .Skip(1)
                .Any())
        {
            caseTarget = null;
            errorMessage = "OFICIO coincide con mas de un registro existente. Revisa el formato del numero de oficio.";
            return false;
        }

        var normalizedNivel = NormalizeLookupKey(nivelEducativo);
        var matches = candidateOfficeCases
            .Where(item => string.Equals(NormalizeLookupKey(item.NivelEducativo), normalizedNivel, StringComparison.Ordinal))
            .OrderBy(item => item.Consecutivo)
            .ToList();

        if (matches.Count == 0)
        {
            caseTarget = null;
            errorMessage = $"OFICIO no tiene un caso para NIVEL '{nivelEducativo}'.";
            return false;
        }

        if (matches.Count > 1)
        {
            caseTarget = null;
            errorMessage = $"OFICIO tiene mas de un caso para NIVEL '{nivelEducativo}'.";
            return false;
        }

        caseTarget = matches[0];
        errorMessage = null;
        return true;
    }

    private static string NormalizeLookupKey(string? value)
    {
        return ImportParsing.NormalizeHeader(value);
    }

    private sealed class CaseImportTarget
    {
        public int OficioId { get; init; }
        public int CasoCorreccionId { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public int Consecutivo { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
        public string AuditoriaEstado { get; set; } = CatalogosAuditoriaCaso.Pendiente;
    }
}
