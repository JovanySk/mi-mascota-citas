using System.Globalization;
using Microsoft.Extensions.Options;
using MiMascota.Web.Options;

namespace MiMascota.Web.Services;

/// <summary>One row of the weekly schedule table.</summary>
public record DayHours(DayOfWeek Day, string Name, bool IsClosed, TimeOnly Opening, TimeOnly Closing, bool IsToday)
{
    /// <summary>How many appointment slots fit in the day.</summary>
    public int Slots(int slotMinutes) =>
        IsClosed ? 0 : (int)((Closing.ToTimeSpan() - Opening.ToTimeSpan()).TotalMinutes / slotMinutes);
}

/// <summary>"Abierto ahora / Cerrado" plus a short explanation, e.g. "Cerramos a las 18:00".</summary>
public record OpenStatus(bool IsOpen, DateTime ClinicNow, string Detail)
{
    public string Label => IsOpen ? "Abierto ahora" : "Cerrado";
}

public interface IClinicHours
{
    OpenStatus GetStatus();
    OpenStatus GetStatus(DateTimeOffset now);
    IReadOnlyList<DayHours> Week();
    IReadOnlyList<DayHours> Week(DateTimeOffset now);
}

/// <summary>
/// Opening hours as the clinic sees them: "now" is converted to the clinic's own
/// time zone (America/Mexico_City), whatever the server's zone is.
/// </summary>
public class ClinicHours(IOptions<ClinicOptions> options, TimeProvider time) : IClinicHours
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    // Monday first, as people read a week in Mexico.
    private static readonly DayOfWeek[] MondayFirst =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private ClinicOptions Clinic => options.Value;

    public OpenStatus GetStatus() => GetStatus(time.GetUtcNow());

    public OpenStatus GetStatus(DateTimeOffset now)
    {
        var local = ToClinicTime(now);
        var today = DateOnly.FromDateTime(local);
        var clock = TimeOnly.FromDateTime(local);
        var opensToday = !Clinic.ClosedDays.Contains(today.DayOfWeek);

        if (opensToday && clock >= Clinic.OpeningTime && clock < Clinic.ClosingTime)
        {
            return new OpenStatus(true, local, $"Cerramos hoy a las {Clinic.ClosingTime:H\\:mm}.");
        }

        if (opensToday && clock < Clinic.OpeningTime)
        {
            return new OpenStatus(false, local, $"Abrimos hoy a las {Clinic.OpeningTime:H\\:mm}.");
        }

        // Closed for the rest of the day: look for the next open day in the coming week.
        for (var i = 1; i <= 7; i++)
        {
            var day = today.AddDays(i);
            if (Clinic.ClosedDays.Contains(day.DayOfWeek)) continue;

            var when = i == 1 ? "mañana" : "el " + Spanish.DateTimeFormat.GetDayName(day.DayOfWeek);
            return new OpenStatus(false, local, $"Abrimos {when} a las {Clinic.OpeningTime:H\\:mm}.");
        }

        return new OpenStatus(false, local, "Por ahora no tenemos días de atención.");
    }

    public IReadOnlyList<DayHours> Week() => Week(time.GetUtcNow());

    public IReadOnlyList<DayHours> Week(DateTimeOffset now)
    {
        var today = ToClinicTime(now).DayOfWeek;
        return MondayFirst
            .Select(day => new DayHours(
                day,
                Capitalize(Spanish.DateTimeFormat.GetDayName(day)),
                Clinic.ClosedDays.Contains(day),
                Clinic.OpeningTime,
                Clinic.ClosingTime,
                day == today))
            .ToList();
    }

    private DateTime ToClinicTime(DateTimeOffset now) =>
        TimeZoneInfo.ConvertTime(now, ResolveTimeZone(Clinic.TimeZoneId)).DateTime;

    /// <summary>IANA id first (works on Linux and on Windows with ICU), then the Windows id, then UTC-6.</summary>
    internal static TimeZoneInfo ResolveTimeZone(string id)
    {
        foreach (var candidate in new[] { id, "Central Standard Time (Mexico)" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // try the next one
            }
        }

        // Mexico City has no daylight saving time since 2022.
        return TimeZoneInfo.CreateCustomTimeZone("Clinic", TimeSpan.FromHours(-6), "Clínica", "Clínica");
    }

    private static string Capitalize(string text) => char.ToUpper(text[0], Spanish) + text[1..];
}
