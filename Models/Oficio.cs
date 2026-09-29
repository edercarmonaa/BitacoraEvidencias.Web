using System.ComponentModel.DataAnnotations;

namespace BitacoraEvidencias.Web.Models;

public class Oficio
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El número de oficio es obligatorio.")]
    [StringLength(80)]
    [Display(Name = "Número de oficio")]
    public string NumeroOficio { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha es obligatoria.")]
    [Display(Name = "Fecha del oficio")]
    [DataType(DataType.Date)]
    public DateTime FechaOficio { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "El asunto es obligatorio.")]
    [StringLength(200)]
    public string Asunto { get; set; } = string.Empty;

    [Display(Name = "Notas")]
    [StringLength(255, ErrorMessage = "Notas excede 255 caracteres.")]
    public string? Notas { get; set; }

    [Display(Name = "Creado")]
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<CasoCorreccion> Casos { get; set; } = new List<CasoCorreccion>();
}
