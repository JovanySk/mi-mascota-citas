using System.ComponentModel.DataAnnotations;

namespace MiMascota.Web.Models;

/// <summary>A client of the clinic (the pet owner).</summary>
public class Owner
{
    public int Id { get; set; }

    [Display(Name = "Nombre completo")]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede tener más de {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Teléfono")]
    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [Phone(ErrorMessage = "Escribe un teléfono válido.")]
    [StringLength(20, ErrorMessage = "El teléfono no puede tener más de {1} caracteres.")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Correo electrónico")]
    [EmailAddress(ErrorMessage = "Escribe un correo electrónico válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede tener más de {1} caracteres.")]
    public string? Email { get; set; }

    public List<Pet> Pets { get; set; } = [];
}
