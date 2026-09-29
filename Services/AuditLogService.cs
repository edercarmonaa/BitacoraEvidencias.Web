using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;

namespace BitacoraEvidencias.Web.Services;

public class AuditLogService(
    AppDbContext dbContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry.EventType))
        {
            return;
        }

        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            var principal = httpContext?.User;

            var username = Normalize(entry.Username) ?? Normalize(principal?.Identity?.Name);
            var role = Normalize(entry.Role) ?? Normalize(principal?.FindFirstValue(ClaimTypes.Role));
            var userAgent = Normalize(entry.UserAgent) ?? Normalize(httpContext?.Request.Headers.UserAgent.ToString());
            var ipAddress = Normalize(entry.IpAddress) ?? Normalize(httpContext?.Connection.RemoteIpAddress?.ToString());

            var userId = entry.UserId;
            if (!userId.HasValue)
            {
                var userIdClaim = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdClaim, out var parsedUserId))
                {
                    userId = parsedUserId;
                }
            }

            var log = new AuditLog
            {
                OccurredAtUtc = entry.OccurredAtUtc?.ToUniversalTime() ?? DateTime.UtcNow,
                EventType = entry.EventType.Trim(),
                EntityType = Normalize(entry.EntityType),
                EntityId = entry.EntityId,
                UserId = userId,
                Username = username,
                Role = role,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                NumeroOficio = Normalize(entry.NumeroOficio),
                Consecutivo = entry.Consecutivo,
                Curp = Normalize(entry.Curp),
                Cct = Normalize(entry.Cct),
                FolioCertificado = Normalize(entry.FolioCertificado),
                EvidenciasCount = entry.EvidenciasCount,
                Details = Normalize(entry.Details),
                Success = entry.Success
            };

            dbContext.AuditLogs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "No fue posible persistir la auditoria. EventType={EventType}, EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}",
                entry.EventType,
                entry.EntityType,
                entry.EntityId,
                entry.UserId);

            // Do not break business flow if audit persistence fails.
        }
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
