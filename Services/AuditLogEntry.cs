namespace BitacoraEvidencias.Web.Services;

public class AuditLogEntry
{
    public string EventType { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? NumeroOficio { get; set; }
    public int? Consecutivo { get; set; }
    public string? Curp { get; set; }
    public string? Cct { get; set; }
    public string? FolioCertificado { get; set; }
    public int? EvidenciasCount { get; set; }
    public string? Details { get; set; }
    public bool Success { get; set; } = true;
    public DateTime? OccurredAtUtc { get; set; }
}
