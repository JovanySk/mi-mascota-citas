namespace MiMascota.Web.Models;

public class Appointment
{
    public int Id { get; set; }

    public int PetId { get; set; }
    public Pet? Pet { get; set; }

    public int VetServiceId { get; set; }
    public VetService? VetService { get; set; }

    /// <summary>Start date and time (clinic local time).</summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// End date and time. Computed from the service duration when the appointment is created,
    /// and stored so overlap checks can run as a simple SQL query.
    /// </summary>
    public DateTime End { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;

    public string? Notes { get; set; }
}
