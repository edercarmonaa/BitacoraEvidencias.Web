using System.ComponentModel.DataAnnotations;

namespace BitacoraEvidencias.Web.Models;

public class EvidenciaFoto
{
    public int Id { get; set; }

    public int CasoCorreccionId { get; set; }
    public CasoCorreccion? CasoCorreccion { get; set; }

    [Required]
    [StringLength(500)]
    [Display(Name = "Ruta archivo")]
    public string RutaArchivo { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    [Display(Name = "Ruta miniatura")]
    public string RutaMiniatura { get; set; } = string.Empty;

    [Required]
    [CustomValidation(typeof(CatalogosCaso), nameof(CatalogosCaso.ValidateTipoEvidencia))]
    [StringLength(80)]
    [Display(Name = "Tipo de evidencia")]
    public string TipoEvidencia { get; set; } = "Extra";

    [Display(Name = "Fecha evidencia")]
    public DateTime FechaEvidencia { get; set; } = DateTime.Today;

    [Display(Name = "Notas")]
    public string? Notas { get; set; }
}
