namespace MiMascota.Web.Models;

/// <summary>A message sent from the public "Contacto" page.</summary>
public class ContactMessage
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Phone number or e-mail, whichever the person wrote.</summary>
    public string Contact { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public bool IsRead { get; set; }
}
