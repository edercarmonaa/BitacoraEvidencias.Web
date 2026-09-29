using System.ComponentModel.DataAnnotations;

namespace BitacoraEvidencias.Web.Models;

public class CasoCorreccion
{
    public int Id { get; set; }

    public int OficioId { get; set; }
    public Oficio? Oficio { get; set; }

    [Display(Name = "Consecutivo")]
    public int Consecutivo { get; set; }

    [StringLength(18)]
    [Display(Name = "CURP")]
    public string? Curp { get; set; }

    [StringLength(64)]
    public string? CurpHash { get; set; }

    [StringLength(140)]
    [Display(Name = "Nombre")]
    public string? NombreCompleto { get; set; }

    [StringLength(30)]
    [Display(Name = "CCT")]
    public string? Cct { get; set; }

    [StringLength(64)]
    public string? CctHash { get; set; }

    [StringLength(140)]
    [Display(Name = "Validador")]
    public string? Validador { get; set; }

    [Display(Name = "Auditor")]
    public int? AuditorUsuarioId { get; set; }

    public UsuarioSistema? AuditorUsuario { get; set; }

    [Required(ErrorMessage = "El nivel educativo es obligatorio.")]
    [CustomValidation(typeof(CatalogosCaso), nameof(CatalogosCaso.ValidateNivelEducativo))]
    [StringLength(20)]
    [Display(Name = "Nivel educativo")]
    public string NivelEducativo { get; set; } = CatalogosCaso.NivelesEducativos[0];

    [Required(ErrorMessage = "El tipo de correccion es obligatorio.")]
    [StringLength(80)]
    [Display(Name = "Tipo de correccion")]
    public string TipoCorreccion { get; set; } = string.Empty;

    [StringLength(80)]
    [Display(Name = "Folio certificado")]
    public string? Folio { get; set; }

    [StringLength(64)]
    public string? FolioHash { get; set; }

    [Display(Name = "Periodo inicio")]
    public int? PeriodoInicio { get; set; }

    [Display(Name = "Periodo fin")]
    public int? PeriodoFin { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de verificacion")]
    public DateTime? FechaVerificacion { get; set; }

    [StringLength(1000)]
    [Display(Name = "Evidencia (liga)")]
    public string? EvidenciaUrl { get; set; }

    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    [Required(ErrorMessage = "El estatus es obligatorio.")]
    [CustomValidation(typeof(CatalogosCaso), nameof(CatalogosCaso.ValidateEstatus))]
    [StringLength(40)]
    [Display(Name = "Estatus")]
    public string Estatus { get; set; } = CatalogosCaso.Estatus[0];

    [Required(ErrorMessage = "El estado de auditoria es obligatorio.")]
    [CustomValidation(typeof(CatalogosAuditoriaCaso), nameof(CatalogosAuditoriaCaso.ValidateEstado))]
    [StringLength(20)]
    [Display(Name = "Auditoria")]
    public string AuditoriaEstado { get; set; } = CatalogosAuditoriaCaso.Pendiente;

    [StringLength(20)]
    [Display(Name = "Ultimo resultado de auditoria")]
    public string? AuditoriaUltimoResultado { get; set; }

    [StringLength(100)]
    [Display(Name = "Ultima observacion de auditoria")]
    public string? AuditoriaUltimaObservacion { get; set; }

    [Display(Name = "Ultimo auditor")]
    public int? AuditoriaUltimoUsuarioId { get; set; }

    [StringLength(60)]
    [Display(Name = "Ultimo auditor")]
    public string? AuditoriaUltimoUsuario { get; set; }

    [Display(Name = "Ultima revision de auditoria")]
    public DateTime? AuditoriaUltimaRevisionUtc { get; set; }

    [Display(Name = "Creado")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<EvidenciaFoto> Evidencias { get; set; } = new List<EvidenciaFoto>();

    public string ConsecutivoTexto => Consecutivo.ToString("000");
}
