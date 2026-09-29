using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface IOficioApplicationService
{
    Task<OficioUpdateResult> UpdateAsync(OficioUpdateRequest request);
    Task<OficioDeleteResult> DeleteAsync(int id);
}

public sealed class OficioUpdateRequest
{
    public int Id { get; init; }
    public string NumeroOficio { get; init; } = string.Empty;
    public DateTime FechaOficio { get; init; }
    public string Asunto { get; init; } = string.Empty;
    public string? Notas { get; init; }
}

public sealed class OficioUpdateResult : ApplicationCommandResult
{
    public int OficioId { get; init; }
    public string NumeroOficio { get; init; } = string.Empty;
}

public sealed class OficioDeleteResult : ApplicationCommandResult
{
    public string NumeroOficio { get; init; } = string.Empty;
}

public sealed class OficioApplicationService(
    AppDbContext dbContext,
    IPhotoStorageService photoStorageService,
    IAuditLogService auditLogService,
    ICaseAuditService caseAuditService) : IOficioApplicationService
{
    public async Task<OficioUpdateResult> UpdateAsync(OficioUpdateRequest request)
    {
        var oficio = await dbContext.Oficios
            .Include(x => x.Casos)
            .ThenInclude(x => x.Evidencias)
            .FirstOrDefaultAsync(x => x.Id == request.Id);

        if (oficio is null)
        {
            return new OficioUpdateResult { NotFound = true };
        }

        var numeroNormalizado = request.NumeroOficio.Trim();
        var numeroCambio = !string.Equals(oficio.NumeroOficio, numeroNormalizado, StringComparison.Ordinal);
        if (numeroCambio)
        {
            var existingNumbers = await dbContext.Oficios
                .AsNoTracking()
                .Where(x => x.Id != oficio.Id)
                .Select(x => x.NumeroOficio)
                .ToListAsync();

            if (existingNumbers.Contains(numeroNormalizado, StringComparer.Ordinal))
            {
                return new OficioUpdateResult
                {
                    ValidationErrors = [new(nameof(OficioUpdateRequest.NumeroOficio), "Ya existe un oficio con ese numero.")]
                };
            }

            if (OficioStoragePath.TryFindFolderCollision(numeroNormalizado, existingNumbers, out var conflictingNumero))
            {
                return new OficioUpdateResult
                {
                    ValidationErrors =
                    [
                        new(
                            nameof(OficioUpdateRequest.NumeroOficio),
                            $"El numero de oficio colisiona con la carpeta de '{conflictingNumero}'. Usa un valor que produzca una carpeta distinta.")
                    ]
                };
            }
        }

        var normalizedAsunto = request.Asunto.Trim();
        var normalizedNotas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim();
        var fechaOficio = request.FechaOficio.Date;
        var numeroAnterior = oficio.NumeroOficio;
        var oficioChanged =
            numeroCambio ||
            oficio.FechaOficio.Date != fechaOficio ||
            !string.Equals(oficio.Asunto, normalizedAsunto, StringComparison.Ordinal) ||
            !string.Equals(oficio.Notas, normalizedNotas, StringComparison.Ordinal);

        if (numeroCambio)
        {
            if (!photoStorageService.TryRenameOficioFolder(numeroAnterior, numeroNormalizado, out var error))
            {
                return new OficioUpdateResult
                {
                    ErrorMessage = error ?? "No se pudo renombrar la carpeta fisica del oficio."
                };
            }

            foreach (var evidencia in oficio.Casos.SelectMany(c => c.Evidencias))
            {
                evidencia.RutaArchivo = photoStorageService.RebaseRelativePathForOficio(
                    evidencia.RutaArchivo,
                    numeroAnterior,
                    numeroNormalizado);
                evidencia.RutaMiniatura = photoStorageService.RebaseRelativePathForOficio(
                    evidencia.RutaMiniatura,
                    numeroAnterior,
                    numeroNormalizado);
            }
        }

        var auditResetCases = new List<CasoCorreccion>();
        if (oficioChanged)
        {
            foreach (var caso in oficio.Casos)
            {
                if (caseAuditService.ResetToPending(caso))
                {
                    auditResetCases.Add(caso);
                }
            }
        }

        oficio.NumeroOficio = numeroNormalizado;
        oficio.FechaOficio = fechaOficio;
        oficio.Asunto = normalizedAsunto;
        oficio.Notas = normalizedNotas;

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation("Oficios.NumeroOficio"))
        {
            if (numeroCambio)
            {
                photoStorageService.TryRenameOficioFolder(numeroNormalizado, numeroAnterior, out _);
            }

            return new OficioUpdateResult
            {
                ValidationErrors = [new(nameof(OficioUpdateRequest.NumeroOficio), "Ya existe un oficio con ese numero.")]
            };
        }
        catch (DbUpdateException)
        {
            if (numeroCambio)
            {
                photoStorageService.TryRenameOficioFolder(numeroNormalizado, numeroAnterior, out _);
            }

            return new OficioUpdateResult
            {
                ErrorMessage = "No se pudieron guardar los cambios del oficio."
            };
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Update,
            EntityType = "Oficio",
            EntityId = oficio.Id,
            NumeroOficio = oficio.NumeroOficio,
            Details = $"Modificacion de oficio. Numero anterior: {numeroAnterior}.",
            Success = true
        });

        foreach (var caso in auditResetCases)
        {
            await caseAuditService.WriteResetLogAsync(
                caso,
                $"La auditoria regreso a pendiente por modificacion del oficio {oficio.NumeroOficio}.");
        }

        return new OficioUpdateResult
        {
            OficioId = oficio.Id,
            NumeroOficio = oficio.NumeroOficio
        };
    }

    public async Task<OficioDeleteResult> DeleteAsync(int id)
    {
        var oficio = await dbContext.Oficios
            .Include(x => x.Casos)
            .ThenInclude(c => c.Evidencias)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (oficio is null)
        {
            return new OficioDeleteResult { NotFound = true };
        }

        var numeroOficio = oficio.NumeroOficio;
        var totalCasos = oficio.Casos.Count;
        var totalEvidencias = oficio.Casos.SelectMany(c => c.Evidencias).Count();
        if (!photoStorageService.TryStageDeleteOficioFolder(numeroOficio, out var stagedDeletion, out var stagingError))
        {
            return new OficioDeleteResult
            {
                ErrorMessage = stagingError ?? "No fue posible preparar la eliminacion fisica del oficio."
            };
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        dbContext.EvidenciasFoto.RemoveRange(oficio.Casos.SelectMany(c => c.Evidencias));
        dbContext.CasosCorreccion.RemoveRange(oficio.Casos);
        dbContext.Oficios.Remove(oficio);

        try
        {
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            if (!photoStorageService.TryRollbackStagedOficioFolderDeletion(stagedDeletion, out var rollbackError))
            {
                return new OficioDeleteResult
                {
                    ErrorMessage = rollbackError ?? "No fue posible revertir la eliminacion fisica del oficio."
                };
            }

            return new OficioDeleteResult
            {
                ErrorMessage = "No fue posible eliminar el oficio. Intenta nuevamente."
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            photoStorageService.TryRollbackStagedOficioFolderDeletion(stagedDeletion, out _);
            return new OficioDeleteResult
            {
                ErrorMessage = "No fue posible eliminar el oficio. Intenta nuevamente."
            };
        }

        string? warningMessage = null;
        if (!photoStorageService.TryFinalizeStagedOficioFolderDeletion(stagedDeletion, out var finalizeError))
        {
            warningMessage = finalizeError;
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Delete,
            EntityType = "Oficio",
            EntityId = id,
            NumeroOficio = numeroOficio,
            EvidenciasCount = totalEvidencias,
            Details = $"Baja definitiva de oficio. Casos eliminados: {totalCasos}.",
            Success = true
        });

        return new OficioDeleteResult
        {
            NumeroOficio = numeroOficio,
            WarningMessage = warningMessage
        };
    }
}
