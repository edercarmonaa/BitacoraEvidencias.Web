namespace BitacoraEvidencias.Web.Services;

public interface IAuditLogService
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}
