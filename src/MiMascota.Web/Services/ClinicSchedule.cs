using System.Globalization;
using MiMascota.Web.Options;

namespace MiMascota.Web.Services;

/// <summary>
/// Schedule rules shared by availability and booking: opening hours,
/// closed days and the slot grid. Pure logic, no database access.
/// </summary>
public class ClinicSchedule(ClinicOptions options)
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-MX");

    public TimeOnly OpeningTime => options.OpeningTime;
    public TimeOnly ClosingTime => options.ClosingTime;
    public int SlotMinutes => options.SlotMinutes;

    public bool IsClosed(DateOnly date) => options.ClosedDays.Contains(date.DayOfWeek);

    public string ClosedDayMessage(DateOnly date)
    {
        // "domingo" -> "domingos", "lunes" -> "lunes"
        var dayName = Spanish.DateTimeFormat.GetDayName(date.DayOfWeek);
        var plural = dayName.EndsWith('s') ? dayName : dayName + "s";
        return $"La clínica no abre los {plural}. Elige otra fecha.";
    }

    /// <summary>
    /// Every possible start time on the grid for a service of the given duration,
    /// making sure the appointment ends before closing time.
    /// </summary>
    public IEnumerable<DateTime> SlotStarts(DateOnly date, int durationMinutes)
    {
        var start = date.ToDateTime(OpeningTime);
        var close = date.ToDateTime(ClosingTime);

        for (var slot = start; slot.AddMinutes(durationMinutes) <= close; slot = slot.AddMinutes(SlotMinutes))
        {
            yield return slot;
        }
    }

    /// <summary>Returns an error message if the start time breaks a schedule rule, otherwise null.</summary>
    public string? ValidateStart(DateTime start, int durationMinutes)
    {
        var date = DateOnly.FromDateTime(start);
        if (IsClosed(date))
        {
            return ClosedDayMessage(date);
        }

        var opening = date.ToDateTime(OpeningTime);
        var closing = date.ToDateTime(ClosingTime);
        var end = start.AddMinutes(durationMinutes);

        if (start < opening || end > closing)
        {
            return $"La cita debe quedar dentro del horario de atención ({OpeningTime:HH\\:mm}–{ClosingTime:HH\\:mm}). " +
                   $"Este servicio dura {durationMinutes} minutos.";
        }

        if ((start - opening).TotalMinutes % SlotMinutes != 0)
        {
            return $"La hora de inicio debe coincidir con los intervalos de {SlotMinutes} minutos de la agenda.";
        }

        return null;
    }
}
