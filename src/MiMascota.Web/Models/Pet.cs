using System.ComponentModel.DataAnnotations;

namespace MiMascota.Web.Models;

public class Pet
{
    public int Id { get; set; }

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "El nombre de la mascota es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede tener más de {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Especie")]
    [Required(ErrorMessage = "La especie es obligatoria.")]
    [StringLength(40, ErrorMessage = "La especie no puede tener más de {1} caracteres.")]
    public string Species { get; set; } = string.Empty;

    [Display(Name = "Raza")]
    [StringLength(60, ErrorMessage = "La raza no puede tener más de {1} caracteres.")]
    public string? Breed { get; set; }

    [Display(Name = "Dueño")]
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona el dueño de la mascota.")]
    public int OwnerId { get; set; }

    /// <summary>Web path of the uploaded photo (e.g. "/uploads/mascotas/{guid}.jpg"). Null = use the illustration.</summary>
    [StringLength(200)]
    public string? PhotoPath { get; set; }

    public Owner? Owner { get; set; }

    public List<Appointment> Appointments { get; set; } = [];
}
