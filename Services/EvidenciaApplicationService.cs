using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface IEvidenciaApplicationService
{
    Task<EvidenciaUpdateResult> UpdateAsync(EvidenciaUpdateRequest request);
    Task<EvidenciaDeleteResult> DeleteAsync(int id);
}

public sealed class EvidenciaUpdateRequest
{
    public int Id { get; init; }
    public string TipoEvidencia { get; init; } = string.Empty;
    public DateTime FechaEvidencia { get; init; }
    public string? Notas { get; init; }
    public IFormFile? ArchivoReemplazo { get; init; }
}

public sealed class EvidenciaUpdateResult : ApplicationCommandResult
{
    public int CasoId { get; init; }
}

public sealed class EvidenciaDeleteResult : ApplicationCommandResult
{
    public int CasoId { get; init; }
}

public sealed class EvidenciaApplicationService(
    AppDbContext dbContext,
    IPhotoStorageService photoStorageService,
    IAuditLogService auditLogService,
    ICaseAuditService caseAuditService) : IEvidenciaApplicationService
{
    public async Task<EvidenciaUpdateResult> UpdateAsync(EvidenciaUpdateRequest request)
    {
        var evidencia = await dbContext.EvidenciasFoto
            .Include(x => x.CasoCorreccion)
            .ThenInclude(x => x!.Oficio)
            .FirstOrDefaultAsync(x => x.Id == request.Id);

        if (evidencia is null || evidencia.CasoCorreccion?.Oficio is null)
        {
            return new EvidenciaUpdateResult { NotFound = true };
        }

        var reemplazoImagen = request.ArchivoReemplazo is not null;
        if (reemplazoImagen && request.ArchivoReemplazo!.Length <= 0)
        {
            return new EvidenciaUpdateResult
            {
                CasoId = evidencia.CasoCorreccionId,
                ValidationErrors = [new(nameof(EvidenciaUpdateRequest.ArchivoReemplazo), "Selecciona una imagen para reemplazar.")]
            };
        }

        var auditResetApplied = false;
        if (reemplazoImagen)
        {
            StoredPhotoInfo nuevaImagen;
            var rutaArchivoAnterior = evidencia.RutaArchivo;
            var rutaMiniaturaAnterior = evidencia.RutaMiniatura;
            try
            {
                nuevaImagen = await photoStorageService.SaveEvidenceAsync(
                    request.ArchivoReemplazo!,
                    evidencia.CasoCorreccion.Oficio.NumeroOficio,
                    evidencia.CasoCorreccion.Consecutivo);
            }
            catch (InvalidOperationException ex)
            {
                return new EvidenciaUpdateResult
                {
                    CasoId = evidencia.CasoCorreccionId,
                    ValidationErrors = [new(nameof(EvidenciaUpdateRequest.ArchivoReemplazo), ex.Message)]
                };
            }

            evidencia.RutaArchivo = nuevaImagen.RelativeFilePath;
            evidencia.RutaMiniatura = nuevaImagen.RelativeThumbnailPath;
            evidencia.TipoEvidencia = CatalogosCaso.TiposEvidencia[0];
            evidencia.FechaEvidencia = DateTime.Today;
            evidencia.Notas = null;
            auditResetApplied = caseAuditService.ResetToPending(evidencia.CasoCorreccion);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                photoStorageService.DeleteEvidenceFiles(nuevaImagen.RelativeFilePath, nuevaImagen.RelativeThumbnailPath);
                evidencia.RutaArchivo = rutaArchivoAnterior;
                evidencia.RutaMiniatura = rutaMiniaturaAnterior;
                return new EvidenciaUpdateResult
                {
                    CasoId = evidencia.CasoCorreccionId,
                    ErrorMessage = "No fue posible reemplazar la evidencia. Intenta nuevamente."
                };
            }

            photoStorageService.DeleteEvidenceFiles(rutaArchivoAnterior, rutaMiniaturaAnterior);
        }
        else
        {
            var normalizedTipoEvidencia = request.TipoEvidencia.Trim();
            var normalizedNotas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim();
            var metadataChanged =
                !string.Equals(evidencia.TipoEvidencia, normalizedTipoEvidencia, StringComparison.Ordinal) ||
                evidencia.FechaEvidencia != request.FechaEvidencia ||
                !string.Equals(evidencia.Notas, normalizedNotas, StringComparison.Ordinal);

            evidencia.TipoEvidencia = normalizedTipoEvidencia;
            evidencia.FechaEvidencia = request.FechaEvidencia;
            evidencia.Notas = normalizedNotas;
            auditResetApplied = metadataChanged && caseAuditService.ResetToPending(evidencia.CasoCorreccion);

            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return new EvidenciaUpdateResult
                {
                    CasoId = evidencia.CasoCorreccionId,
                    ErrorMessage = "No fue posible actualizar la evidencia. Intenta nuevamente."
                };
            }
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = reemplazoImagen ? AuditEvents.UploadEvidence : AuditEvents.Update,
            EntityType = "Evidencia",
            EntityId = evidencia.Id,
            NumeroOficio = evidencia.CasoCorreccion.Oficio.NumeroOficio,
            Consecutivo = evidencia.CasoCorreccion.Consecutivo,
            Curp = evidencia.CasoCorreccion.Curp,
            Cct = evidencia.CasoCorreccion.Cct,
            FolioCertificado = evidencia.CasoCorreccion.Folio,
            Details = reemplazoImagen
                ? "Reemplazo de archivo de evidencia."
                : "Modificacion de metadatos de evidencia.",
            Success = true
        });

        if (auditResetApplied)
        {
            await caseAuditService.WriteResetLogAsync(
                evidencia.CasoCorreccion,
                reemplazoImagen
                    ? "La auditoria regreso a pendiente por reemplazo de archivo de evidencia."
                    : "La auditoria regreso a pendiente por modificacion de metadatos de evidencia.");
        }

        return new EvidenciaUpdateResult
        {
            CasoId = evidencia.CasoCorreccionId
        };
    }

    public async Task<EvidenciaDeleteResult> DeleteAsync(int id)
    {
        var evidencia = await dbContext.EvidenciasFoto
            .Include(x => x.CasoCorreccion)
            .ThenInclude(x => x!.Oficio)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (evidencia is null || evidencia.CasoCorreccion?.Oficio is null)
        {
            return new EvidenciaDeleteResult { NotFound = true };
        }

        var numeroOficio = evidencia.CasoCorreccion.Oficio.NumeroOficio;
        var consecutivo = evidencia.CasoCorreccion.Consecutivo;
        var curp = evidencia.CasoCorreccion.Curp;
        var cct = evidencia.CasoCorreccion.Cct;
        var folio = evidencia.CasoCorreccion.Folio;
        var casoId = evidencia.CasoCorreccionId;
        var rutaArchivo = evidencia.RutaArchivo;
        var rutaMiniatura = evidencia.RutaMiniatura;
        if (!photoStorageService.TryStageDeleteEvidenceFiles(rutaArchivo, rutaMiniatura, out var stagedDeletion, out var stagingError))
        {
            return new EvidenciaDeleteResult
            {
                CasoId = casoId,
                ErrorMessage = stagingError ?? "No fue posible preparar la eliminacion fisica de la evidencia."
            };
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var auditResetApplied = caseAuditService.ResetToPending(evidencia.CasoCorreccion);
        dbContext.EvidenciasFoto.Remove(evidencia);

        try
        {
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            if (!photoStorageService.TryRollbackStagedEvidenceDeletion(stagedDeletion, out var rollbackError))
            {
                return new EvidenciaDeleteResult
                {
                    CasoId = casoId,
                    ErrorMessage = rollbackError ?? "No fue posible revertir la eliminacion fisica de la evidencia."
                };
            }

            return new EvidenciaDeleteResult
            {
                CasoId = casoId,
                ErrorMessage = "No fue posible eliminar la evidencia. Intenta nuevamente."
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            photoStorageService.TryRollbackStagedEvidenceDeletion(stagedDeletion, out _);
            return new EvidenciaDeleteResult
            {
                CasoId = casoId,
                ErrorMessage = "No fue posible eliminar la evidencia. Intenta nuevamente."
            };
        }

        string? warningMessage = null;
        if (!photoStorageService.TryFinalizeStagedEvidenceDeletion(stagedDeletion, out var finalizeError))
        {
            warningMessage = finalizeError;
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.DeleteEvidence,
            EntityType = "Evidencia",
            EntityId = id,
            NumeroOficio = numeroOficio,
            Consecutivo = consecutivo,
            Curp = curp,
            Cct = cct,
            FolioCertificado = folio,
            Details = "Eliminacion de evidencia (archivo y base de datos).",
            Success = true
        });

        if (auditResetApplied)
        {
            await caseAuditService.WriteResetLogAsync(
                evidencia.CasoCorreccion,
                "La auditoria regreso a pendiente por eliminacion de evidencia.");
        }

        return new EvidenciaDeleteResult
        {
            CasoId = casoId,
            WarningMessage = warningMessage
        };
    }
}
