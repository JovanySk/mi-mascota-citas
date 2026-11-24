namespace MiMascota.Web.Options;

/// <summary>
/// Clinic schedule, bound from the "Clinic" section of appsettings.json.
/// </summary>
public class ClinicOptions
{
    public const string SectionName = "Clinic";

    public TimeOnly OpeningTime { get; set; } = new(9, 0);

    public TimeOnly ClosingTime { get; set; } = new(18, 0);

    /// <summary>Size of the booking grid. Appointments can only start on this grid.</summary>
    public int SlotMinutes { get; set; } = 30;

    /// <summary>Days the clinic is closed (configured in appsettings.json, e.g. ["Sunday"]).</summary>
    public List<DayOfWeek> ClosedDays { get; set; } = [];
}
