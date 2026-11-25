using System.ComponentModel.DataAnnotations;
using MiMascota.Web.Models;

namespace MiMascota.Web.ViewModels;

/// <summary>The public contact form. The same rules run in the browser (unobtrusive validation) and on the server.</summary>
public class ContactFormViewModel
{
    /// <summary>A phone number (8 to 15 digits, spaces, dashes, parentheses and a leading +) or an e-mail address.</summary>
    public const string PhoneOrEmailPattern =
        @"^\s*(\+?\(?[0-9][0-9 ()\-]{6,18}[0-9]|[^@\s]+@[^@\s]+\.[^@\s]+)\s*$";

    [Display(Name = "Tu nombre")]
    [Required(ErrorMessage = "Escribe tu nombre.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Teléfono o correo")]
    [Required(ErrorMessage = "Déjanos un teléfono o un correo para responderte.")]
    [StringLength(150, ErrorMessage = "El teléfono o correo no puede tener más de {1} caracteres.")]
    [RegularExpression(PhoneOrEmailPattern, ErrorMessage = "Escribe un teléfono (por ejemplo 789 123 4567) o un correo válido.")]
    public string Contact { get; set; } = string.Empty;

    [Display(Name = "Mensaje")]
    [Required(ErrorMessage = "Escribe tu mensaje.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "El mensaje debe tener entre {2} y {1} caracteres.")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Text prefilled when a visitor comes from "Agenda una cita" or a service link.</summary>
    public static string AppointmentRequest(string? serviceName = null) => serviceName is null
        ? "Hola, quiero agendar una cita para mi mascota. ¿Qué horarios tienen disponibles?"
        : $"Hola, quiero agendar una cita de «{serviceName}» para mi mascota. ¿Qué horarios tienen disponibles?";
}

public class ContactMessagesViewModel
{
    public required IReadOnlyList<ContactMessage> Messages { get; init; }
    public int UnreadCount => Messages.Count(m => !m.IsRead);
}
