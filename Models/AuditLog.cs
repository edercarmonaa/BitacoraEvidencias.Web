using System.ComponentModel.DataAnnotations;

namespace BitacoraEvidencias.Web.Models;

public class AuditLog
{
    public long Id { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(60)]
    public string EventType { get; set; } = string.Empty;

    [StringLength(40)]
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }

    public int? UserId { get; set; }

    [StringLength(60)]
    public string? Username { get; set; }

    [StringLength(20)]
    public string? Role { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    [StringLength(80)]
    public string? NumeroOficio { get; set; }

    public int? Consecutivo { get; set; }

    [StringLength(18)]
    public string? Curp { get; set; }

    [StringLength(64)]
    public string? CurpHash { get; set; }

    [StringLength(8)]
    public string? CurpSuffix { get; set; }

    [StringLength(30)]
    public string? Cct { get; set; }

    [StringLength(64)]
    public string? CctHash { get; set; }

    [StringLength(8)]
    public string? CctSuffix { get; set; }

    [StringLength(80)]
    public string? FolioCertificado { get; set; }

    [StringLength(64)]
    public string? FolioCertificadoHash { get; set; }

    [StringLength(8)]
    public string? FolioCertificadoSuffix { get; set; }

    public int? EvidenciasCount { get; set; }

    [StringLength(2000)]
    public string? Details { get; set; }

    public bool Success { get; set; } = true;
}
