using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Options;

namespace MiMascota.Web.Services;

/// <summary>
/// Calculates free slots. The clinic has a single consultation room, so any
/// non-cancelled appointment blocks the time it occupies for every service.
/// </summary>
public class AvailabilityService(AppDbContext db, IOptions<ClinicOptions> options, TimeProvider time)
    : IAvailabilityService
{
    private readonly ClinicSchedule _schedule = new(options.Value);

    public async Task<Result<IReadOnlyList<TimeSlot>>> GetAvailableSlotsAsync(DateOnly date, int serviceId)
    {
        var service = await db.VetServices.FindAsync(serviceId);
        if (service is null)
        {
            return Result<IReadOnlyList<TimeSlot>>.Failure("El servicio seleccionado no existe.");
        }

        var now = time.GetLocalNow().DateTime;
        if (date < DateOnly.FromDateTime(now))
        {
            return Result<IReadOnlyList<TimeSlot>>.Failure("No se pueden agendar citas en fechas pasadas.");
        }

        if (_schedule.IsClosed(date))
        {
            return Result<IReadOnlyList<TimeSlot>>.Failure(_schedule.ClosedDayMessage(date));
        }

        // Time ranges already taken that day (cancelled appointments free their slot).
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var busy = await db.Appointments
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Start < dayEnd && a.End > dayStart)
            .Select(a => new { a.Start, a.End })
            .ToListAsync();

        var slots = _schedule.SlotStarts(date, service.DurationMinutes)
            .Where(start => start > now) // no slots in the past
            .Select(start => new TimeSlot(start, start.AddMinutes(service.DurationMinutes)))
            .Where(slot => !busy.Any(b => b.Start < slot.End && b.End > slot.Start)) // no overlaps
            .ToList();

        return Result<IReadOnlyList<TimeSlot>>.Success(slots);
    }
}
