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

    /// <summary>IANA id of the clinic's time zone, used for "Abierto ahora / Cerrado".</summary>
    public string TimeZoneId { get; set; } = "America/Mexico_City";
}

/// <summary>Short Spanish labels for the opening hours, used by the landing page and the footer.</summary>
public static class ClinicOptionsLabels
{
    private static readonly string[] ShortDayNames = ["Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb"];

    /// <summary>"9:00–18:00"</summary>
    public static string HoursLabel(this ClinicOptions options) =>
        $"{options.OpeningTime.ToString("H:mm")}–{options.ClosingTime.ToString("H:mm")}";

    /// <summary>"Lun a Sáb" when the open days are consecutive (Monday first), otherwise "Lun, Mié, Vie".</summary>
    public static string OpenDaysLabel(this ClinicOptions options)
    {
        // Monday-first week, as people read it in Mexico.
        DayOfWeek[] week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        var open = week.Where(d => !options.ClosedDays.Contains(d)).ToList();

        if (open.Count == 0) return "Cerrado";
        if (open.Count == 7) return "Todos los días";

        var first = Array.IndexOf(week, open[0]);
        var consecutive = open.Select((d, i) => Array.IndexOf(week, d) == first + i).All(x => x);
        return consecutive && open.Count > 2
            ? $"{ShortDayNames[(int)open[0]]} a {ShortDayNames[(int)open[^1]]}"
            : string.Join(", ", open.Select(d => ShortDayNames[(int)d]));
    }
}
