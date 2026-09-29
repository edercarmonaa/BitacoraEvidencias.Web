using System.ComponentModel.DataAnnotations;

namespace BitacoraEvidencias.Web.Models;

public class UsuarioSistema
{
    public int Id { get; set; }

    [Required]
    [StringLength(60)]
    [Display(Name = "Usuario")]
    public string Usuario { get; set; } = string.Empty;

    [Required]
    [StringLength(60)]
    public string UsuarioNormalizado { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string PasswordSalt { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Rol { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public int IntentosFallidos { get; set; }

    public DateTime? BloqueadoHastaUtc { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime CreadoEnUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UltimoAccesoUtc { get; set; }

    public DateTime? PasswordUpdatedAtUtc { get; set; }
}
