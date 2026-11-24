using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Options;

namespace MiMascota.Web.Services;

public class AppointmentService(AppDbContext db, IOptions<ClinicOptions> options, TimeProvider time)
    : IAppointmentService
{
    private readonly ClinicSchedule _schedule = new(options.Value);

    public async Task<IReadOnlyList<Appointment>> ListAsync(DateOnly? date, AppointmentStatus? status)
    {
        var query = db.Appointments
            .Include(a => a.Pet!).ThenInclude(p => p.Owner)
            .Include(a => a.VetService)
            .AsNoTracking();

        if (date is not null)
        {
            var dayStart = date.Value.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(a => a.Start >= dayStart && a.Start < dayEnd);
        }

        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        return await query.OrderBy(a => a.Start).ToListAsync();
    }

    public async Task<Result<Appointment>> CreateAsync(NewAppointment request)
    {
        var pet = await db.Pets.FindAsync(request.PetId);
        if (pet is null)
        {
            return Result<Appointment>.Failure("La mascota seleccionada no existe.");
        }

        var service = await db.VetServices.FindAsync(request.ServiceId);
        if (service is null)
        {
            return Result<Appointment>.Failure("El servicio seleccionado no existe.");
        }

        var start = request.Start;
        var end = start.AddMinutes(service.DurationMinutes);

        if (start <= time.GetLocalNow().DateTime)
        {
            return Result<Appointment>.Failure("No se puede agendar una cita en una fecha u hora que ya pasó.");
        }

        var scheduleError = _schedule.ValidateStart(start, service.DurationMinutes);
        if (scheduleError is not null)
        {
            return Result<Appointment>.Failure(scheduleError);
        }

        // Two time ranges overlap when each one starts before the other ends.
        var conflict = await db.Appointments
            .Include(a => a.Pet)
            .Include(a => a.VetService)
            .Where(a => a.Status != AppointmentStatus.Cancelled && a.Start < end && a.End > start)
            .OrderBy(a => a.Start)
            .FirstOrDefaultAsync();

        if (conflict is not null)
        {
            return Result<Appointment>.Failure(
                $"El horario {start:HH:mm}–{end:HH:mm} ya está ocupado: se empalma con la cita de " +
                $"{conflict.Pet?.Name} ({conflict.VetService?.Name}, {conflict.Start:HH:mm}–{conflict.End:HH:mm}). " +
                "Elige otro horario disponible.");
        }

        var appointment = new Appointment
        {
            PetId = pet.Id,
            VetServiceId = service.Id,
            Start = start,
            End = end,
            Status = AppointmentStatus.Pending,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        return Result<Appointment>.Success(appointment);
    }

    public async Task<Result> ChangeStatusAsync(int appointmentId, AppointmentStatus newStatus)
    {
        var appointment = await db.Appointments.FindAsync(appointmentId);
        if (appointment is null)
        {
            return Result.Failure("La cita no existe.");
        }

        if (!AppointmentStateMachine.CanTransition(appointment.Status, newStatus))
        {
            return Result.Failure(AppointmentStateMachine.DescribeRejection(appointment.Status, newStatus));
        }

        if (newStatus == AppointmentStatus.Confirmed && appointment.Start <= time.GetLocalNow().DateTime)
        {
            return Result.Failure("No se puede confirmar una cita cuya fecha y hora ya pasaron.");
        }

        appointment.Status = newStatus;
        await db.SaveChangesAsync();

        return Result.Success();
    }
}
