using MiMascota.Web.Models;

namespace MiMascota.Web.Services;

public interface IAppointmentService
{
    /// <summary>Appointments ordered by start time, optionally filtered by day and status.</summary>
    Task<IReadOnlyList<Appointment>> ListAsync(DateOnly? date, AppointmentStatus? status);

    /// <summary>Books a new appointment (status Pendiente) after validating schedule and overlaps.</summary>
    Task<Result<Appointment>> CreateAsync(NewAppointment request);

    /// <summary>Moves an appointment to a new status if the state machine allows it.</summary>
    Task<Result> ChangeStatusAsync(int appointmentId, AppointmentStatus newStatus);
}

public record NewAppointment(int PetId, int ServiceId, DateTime Start, string? Notes);
