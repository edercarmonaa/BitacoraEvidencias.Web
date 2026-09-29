using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface ICasoApplicationService
{
    Task<CasoUpdateResult> UpdateAsync(CasoUpdateRequest request);
    Task<CasoDeleteResult> DeleteAsync(int id);
}

public sealed class CasoUpdateRequest
{
    public int Id { get; init; }
    public string? Curp { get; init; }
    public string? NombreCompleto { get; init; }
    public string? Cct { get; init; }
    public string? Validador { get; init; }
    public int? AuditorUsuarioId { get; init; }
    public string NivelEducativo { get; init; } = string.Empty;
    public string TipoCorreccion { get; init; } = string.Empty;
    public string? Folio { get; init; }
    public int? PeriodoInicio { get; init; }
    public int? PeriodoFin { get; init; }
    public DateTime? FechaVerificacion { get; init; }
    public string? EvidenciaUrl { get; init; }
    public string? Observaciones { get; init; }
    public string Estatus { get; init; } = string.Empty;
}

public sealed class CasoUpdateResult : ApplicationCommandResult
{
    public int CasoId { get; init; }
    public int OficioId { get; init; }
    public string ConsecutivoTexto { get; init; } = string.Empty;
}

public sealed class CasoDeleteResult : ApplicationCommandResult
{
    public int RedirectOficioId { get; init; }
    public string ConsecutivoTexto { get; init; } = string.Empty;
}

public sealed class CasoApplicationService(
    AppDbContext dbContext,
    IPhotoStorageService photoStorageService,
    IAuditLogService auditLogService,
    ICaseAuditService caseAuditService) : ICasoApplicationService
{
    public async Task<CasoUpdateResult> UpdateAsync(CasoUpdateRequest request)
    {
        var caso = await dbContext.CasosCorreccion
            .Include(x => x.Oficio)
            .FirstOrDefaultAsync(x => x.Id == request.Id);

        if (caso is null || caso.Oficio is null)
        {
            return new CasoUpdateResult { NotFound = true };
        }

        var validationErrors = new List<ApplicationValidationError>();
        var nivelEducativo = request.NivelEducativo.Trim();

        if (request.PeriodoInicio.HasValue && request.PeriodoFin.HasValue && request.PeriodoFin.Value < request.PeriodoInicio.Value)
        {
            validationErrors.Add(new(nameof(CasoUpdateRequest.PeriodoFin), "El periodo fin no puede ser menor al periodo inicio."));
        }

        if (await dbContext.CasosCorreccion
                .AsNoTracking()
                .AnyAsync(x => x.OficioId == caso.OficioId && x.Id != caso.Id && x.NivelEducativo == nivelEducativo))
        {
            validationErrors.Add(new(nameof(CasoUpdateRequest.NivelEducativo), $"Ya existe un caso de {nivelEducativo} para este oficio."));
        }

        if (request.AuditorUsuarioId.HasValue && !await UserCanBeAssignedAsAuditorAsync(request.AuditorUsuarioId.Value, caso.AuditorUsuarioId))
        {
            validationErrors.Add(new(nameof(CasoUpdateRequest.AuditorUsuarioId), "Selecciona un auditor activo valido."));
        }

        if (validationErrors.Count > 0)
        {
            return new CasoUpdateResult
            {
                ValidationErrors = validationErrors,
                CasoId = caso.Id,
                OficioId = caso.OficioId,
                ConsecutivoTexto = caso.ConsecutivoTexto
            };
        }

        var normalizedCurp = NormalizeOptional(request.Curp);
        var normalizedNombreCompleto = NormalizeOptional(request.NombreCompleto);
        var normalizedCct = NormalizeOptional(request.Cct);
        var normalizedValidador = NormalizeOptional(request.Validador);
        var normalizedFolio = NormalizeOptional(request.Folio);
        var normalizedEvidenciaUrl = NormalizeOptional(request.EvidenciaUrl);
        var normalizedObservaciones = NormalizeOptional(request.Observaciones);
        var normalizedTipoCorreccion = request.TipoCorreccion.Trim();
        var normalizedEstatus = request.Estatus.Trim();

        var caseChanged =
            !string.Equals(caso.Curp, normalizedCurp, StringComparison.Ordinal) ||
            !string.Equals(caso.NombreCompleto, normalizedNombreCompleto, StringComparison.Ordinal) ||
            !string.Equals(caso.Cct, normalizedCct, StringComparison.Ordinal) ||
            !string.Equals(caso.Validador, normalizedValidador, StringComparison.Ordinal) ||
            caso.AuditorUsuarioId != request.AuditorUsuarioId ||
            !string.Equals(caso.NivelEducativo, nivelEducativo, StringComparison.Ordinal) ||
            !string.Equals(caso.TipoCorreccion, normalizedTipoCorreccion, StringComparison.Ordinal) ||
            !string.Equals(caso.Folio, normalizedFolio, StringComparison.Ordinal) ||
            caso.PeriodoInicio != request.PeriodoInicio ||
            caso.PeriodoFin != request.PeriodoFin ||
            caso.FechaVerificacion != request.FechaVerificacion ||
            !string.Equals(caso.EvidenciaUrl, normalizedEvidenciaUrl, StringComparison.Ordinal) ||
            !string.Equals(caso.Observaciones, normalizedObservaciones, StringComparison.Ordinal) ||
            !string.Equals(caso.Estatus, normalizedEstatus, StringComparison.Ordinal);

        caso.Curp = normalizedCurp;
        caso.NombreCompleto = normalizedNombreCompleto;
        caso.Cct = normalizedCct;
        caso.Validador = normalizedValidador;
        caso.AuditorUsuarioId = request.AuditorUsuarioId;
        caso.NivelEducativo = nivelEducativo;
        caso.TipoCorreccion = normalizedTipoCorreccion;
        caso.Folio = normalizedFolio;
        caso.PeriodoInicio = request.PeriodoInicio;
        caso.PeriodoFin = request.PeriodoFin;
        caso.FechaVerificacion = request.FechaVerificacion;
        caso.EvidenciaUrl = normalizedEvidenciaUrl;
        caso.Observaciones = normalizedObservaciones;
        caso.Estatus = normalizedEstatus;

        var auditResetApplied = caseChanged && caseAuditService.ResetToPending(caso);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation("CasosCorreccion.OficioId, CasosCorreccion.NivelEducativo"))
        {
            return new CasoUpdateResult
            {
                ValidationErrors = [new(nameof(CasoUpdateRequest.NivelEducativo), $"Ya existe un caso de {nivelEducativo} para este oficio.")],
                CasoId = caso.Id,
                OficioId = caso.OficioId,
                ConsecutivoTexto = caso.ConsecutivoTexto
            };
        }
        catch (DbUpdateException)
        {
            return new CasoUpdateResult
            {
                ErrorMessage = "No se pudieron guardar los cambios del caso.",
                CasoId = caso.Id,
                OficioId = caso.OficioId,
                ConsecutivoTexto = caso.ConsecutivoTexto
            };
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Update,
            EntityType = "Caso",
            EntityId = caso.Id,
            NumeroOficio = caso.Oficio.NumeroOficio,
            Consecutivo = caso.Consecutivo,
            Curp = caso.Curp,
            Cct = caso.Cct,
            FolioCertificado = caso.Folio,
            Details = "Modificacion de caso.",
            Success = true
        });

        if (auditResetApplied)
        {
            await caseAuditService.WriteResetLogAsync(
                caso,
                "La auditoria regreso a pendiente por modificacion de datos del caso.");
        }

        return new CasoUpdateResult
        {
            CasoId = caso.Id,
            OficioId = caso.OficioId,
            ConsecutivoTexto = caso.ConsecutivoTexto
        };
    }

    public async Task<CasoDeleteResult> DeleteAsync(int id)
    {
        var caso = await dbContext.CasosCorreccion
            .Include(x => x.Oficio)
            .Include(x => x.Evidencias)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (caso is null || caso.Oficio is null)
        {
            return new CasoDeleteResult { NotFound = true };
        }

        var oficioId = caso.OficioId;
        var numeroOficio = caso.Oficio.NumeroOficio;
        var consecutivoEliminado = caso.Consecutivo;
        var curp = caso.Curp;
        var cct = caso.Cct;
        var folio = caso.Folio;
        var totalEvidencias = caso.Evidencias.Count;

        var restantes = await dbContext.CasosCorreccion
            .Include(x => x.Evidencias)
            .Where(x => x.OficioId == oficioId && x.Id != id)
            .OrderBy(x => x.Consecutivo)
            .ToListAsync();

        var moves = new List<(int OldConsecutivo, int NewConsecutivo)>();
        for (var i = 0; i < restantes.Count; i++)
        {
            var nuevoConsecutivo = i + 1;
            if (restantes[i].Consecutivo != nuevoConsecutivo)
            {
                moves.Add((restantes[i].Consecutivo, nuevoConsecutivo));
            }
        }

        StagedCaseFolderMutation? stagedMutation = null;
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            if (!photoStorageService.TryStageDeleteAndReorderCaseFolders(
                    numeroOficio,
                    consecutivoEliminado,
                    moves,
                    out stagedMutation,
                    out var error))
            {
                await transaction.RollbackAsync();
                return new CasoDeleteResult
                {
                    ErrorMessage = error ?? "No fue posible reordenar carpetas de casos.",
                    RedirectOficioId = oficioId,
                    ConsecutivoTexto = consecutivoEliminado.ToString("000")
                };
            }

            dbContext.EvidenciasFoto.RemoveRange(caso.Evidencias);
            dbContext.CasosCorreccion.Remove(caso);
            await dbContext.SaveChangesAsync();

            for (var i = 0; i < restantes.Count; i++)
            {
                var casoRestante = restantes[i];
                var nuevoConsecutivo = i + 1;
                if (casoRestante.Consecutivo == nuevoConsecutivo)
                {
                    continue;
                }

                var consecutivoAnterior = casoRestante.Consecutivo;
                foreach (var evidencia in casoRestante.Evidencias)
                {
                    evidencia.RutaArchivo = photoStorageService.RebaseRelativePathForCase(
                        evidencia.RutaArchivo,
                        numeroOficio,
                        consecutivoAnterior,
                        nuevoConsecutivo);
                    evidencia.RutaMiniatura = photoStorageService.RebaseRelativePathForCase(
                        evidencia.RutaMiniatura,
                        numeroOficio,
                        consecutivoAnterior,
                        nuevoConsecutivo);
                }

                casoRestante.Consecutivo = nuevoConsecutivo;
            }

            if (moves.Count > 0)
            {
                await dbContext.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            if (!photoStorageService.TryRollbackStagedCaseFolderMutation(stagedMutation, out var rollbackError))
            {
                return new CasoDeleteResult
                {
                    ErrorMessage = rollbackError ?? "No fue posible revertir la reorganizacion fisica del caso.",
                    RedirectOficioId = oficioId,
                    ConsecutivoTexto = consecutivoEliminado.ToString("000")
                };
            }

            return new CasoDeleteResult
            {
                ErrorMessage = "No fue posible eliminar el caso. Intenta nuevamente.",
                RedirectOficioId = oficioId,
                ConsecutivoTexto = consecutivoEliminado.ToString("000")
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            photoStorageService.TryRollbackStagedCaseFolderMutation(stagedMutation, out _);
            return new CasoDeleteResult
            {
                ErrorMessage = "No fue posible eliminar el caso. Intenta nuevamente.",
                RedirectOficioId = oficioId,
                ConsecutivoTexto = consecutivoEliminado.ToString("000")
            };
        }

        string? warningMessage = null;
        if (!photoStorageService.TryFinalizeStagedCaseFolderMutation(stagedMutation, out var finalizeError))
        {
            warningMessage = finalizeError;
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Delete,
            EntityType = "Caso",
            EntityId = id,
            NumeroOficio = numeroOficio,
            Consecutivo = consecutivoEliminado,
            Curp = curp,
            Cct = cct,
            FolioCertificado = folio,
            EvidenciasCount = totalEvidencias,
            Details = "Baja definitiva de caso.",
            Success = true
        });

        return new CasoDeleteResult
        {
            RedirectOficioId = oficioId,
            ConsecutivoTexto = consecutivoEliminado.ToString("000"),
            WarningMessage = warningMessage
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<bool> UserCanBeAssignedAsAuditorAsync(int userId, int? currentAuditorUsuarioId)
    {
        if (currentAuditorUsuarioId == userId)
        {
            return await dbContext.UsuariosSistema
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId);
        }

        return await dbContext.UsuariosSistema
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.Activo);
    }
}
