using System.Security.Claims;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;

namespace BitacoraEvidencias.Web.Services;

public class CaseAuditService(
    IAuditLogService auditLogService,
    IHttpContextAccessor httpContextAccessor) : ICaseAuditService
{
    public void MarkCoincidente(CasoCorreccion caso)
    {
        var (userId, username) = GetCurrentUserContext();
        var reviewedAtUtc = DateTime.UtcNow;

        caso.AuditoriaEstado = CatalogosAuditoriaCaso.Coincidente;
        caso.AuditoriaUltimoResultado = CatalogosAuditoriaCaso.Coincidente;
        caso.AuditoriaUltimaObservacion = null;
        caso.AuditoriaUltimoUsuarioId = userId;
        caso.AuditoriaUltimoUsuario = username;
        caso.AuditoriaUltimaRevisionUtc = reviewedAtUtc;
    }

    public void MarkNoCoincidente(CasoCorreccion caso, string observacion)
    {
        var (userId, username) = GetCurrentUserContext();
        var reviewedAtUtc = DateTime.UtcNow;

        caso.AuditoriaEstado = CatalogosAuditoriaCaso.NoCoincidente;
        caso.AuditoriaUltimoResultado = CatalogosAuditoriaCaso.NoCoincidente;
        caso.AuditoriaUltimaObservacion = observacion.Trim();
        caso.AuditoriaUltimoUsuarioId = userId;
        caso.AuditoriaUltimoUsuario = username;
        caso.AuditoriaUltimaRevisionUtc = reviewedAtUtc;
    }

    public bool ResetToPending(CasoCorreccion caso)
    {
        if (CatalogosAuditoriaCaso.NormalizarEstado(caso.AuditoriaEstado) == CatalogosAuditoriaCaso.Pendiente)
        {
            caso.AuditoriaEstado = CatalogosAuditoriaCaso.Pendiente;
            return false;
        }

        caso.AuditoriaEstado = CatalogosAuditoriaCaso.Pendiente;
        return true;
    }

    public Task WriteDecisionLogAsync(CasoCorreccion caso, CancellationToken cancellationToken = default)
    {
        var details = caso.AuditoriaEstado == CatalogosAuditoriaCaso.NoCoincidente
            ? $"Auditoria marcada como no coincidente. Motivo: {caso.AuditoriaUltimaObservacion}."
            : "Auditoria marcada como coincidente.";

        return auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.AuditCase,
            EntityType = "Caso",
            EntityId = caso.Id,
            NumeroOficio = caso.Oficio?.NumeroOficio,
            Consecutivo = caso.Consecutivo,
            Curp = caso.Curp,
            Cct = caso.Cct,
            FolioCertificado = caso.Folio,
            Details = details,
            Success = true
        }, cancellationToken);
    }

    public Task WriteResetLogAsync(CasoCorreccion caso, string details, CancellationToken cancellationToken = default)
    {
        return auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.ResetCaseAudit,
            EntityType = "Caso",
            EntityId = caso.Id,
            NumeroOficio = caso.Oficio?.NumeroOficio,
            Consecutivo = caso.Consecutivo,
            Curp = caso.Curp,
            Cct = caso.Cct,
            FolioCertificado = caso.Folio,
            Details = details,
            Success = true
        }, cancellationToken);
    }

    private (int? UserId, string? Username) GetCurrentUserContext()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var username = principal?.Identity?.Name?.Trim();
        var userIdClaim = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(userIdClaim, out var userId)
            ? (userId, username)
            : (null, username);
    }
}
