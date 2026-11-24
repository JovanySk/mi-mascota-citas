using System.ComponentModel.DataAnnotations;

namespace MiMascota.Web.Models;

/// <summary>A service offered by the clinic (consultation, vaccination, grooming...).</summary>
public class VetService
{
    public int Id { get; set; }

    [Display(Name = "Servicio")]
    [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no puede tener más de {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Duración (minutos)")]
    [Range(15, 240, ErrorMessage = "La duración debe estar entre {1} y {2} minutos.")]
    public int DurationMinutes { get; set; } = 30;

    [Display(Name = "Precio")]
    [Range(0, 100_000, ErrorMessage = "El precio debe estar entre {1} y {2}.")]
    public decimal Price { get; set; }

    public List<Appointment> Appointments { get; set; } = [];
}
