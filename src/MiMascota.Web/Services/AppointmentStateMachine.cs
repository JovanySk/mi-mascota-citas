using MiMascota.Web.Models;

namespace MiMascota.Web.Services;

/// <summary>
/// Allowed status changes:
///   Pendiente  → Confirmada, Cancelada
///   Confirmada → Atendida, Cancelada
///   Atendida and Cancelada are final states.
/// </summary>
public static class AppointmentStateMachine
{
    private static readonly Dictionary<AppointmentStatus, AppointmentStatus[]> AllowedTransitions = new()
    {
        [AppointmentStatus.Pending] = [AppointmentStatus.Confirmed, AppointmentStatus.Cancelled],
        [AppointmentStatus.Confirmed] = [AppointmentStatus.Attended, AppointmentStatus.Cancelled],
        [AppointmentStatus.Attended] = [],
        [AppointmentStatus.Cancelled] = []
    };

    public static IReadOnlyList<AppointmentStatus> NextStates(AppointmentStatus current) =>
        AllowedTransitions[current];

    public static bool CanTransition(AppointmentStatus from, AppointmentStatus to) =>
        AllowedTransitions[from].Contains(to);

    /// <summary>Explains in Spanish why a status change is not allowed.</summary>
    public static string DescribeRejection(AppointmentStatus from, AppointmentStatus to)
    {
        if (from == to)
        {
            return $"La cita ya está en estado «{to.ToDisplayName()}».";
        }

        return (from, to) switch
        {
            (AppointmentStatus.Attended, _) =>
                "La cita ya fue atendida; su estado ya no puede cambiar.",
            (AppointmentStatus.Cancelled, _) =>
                "La cita está cancelada; su estado ya no puede cambiar. Agenda una nueva cita si es necesario.",
            (AppointmentStatus.Pending, AppointmentStatus.Attended) =>
                "No se puede marcar como atendida una cita pendiente: primero debe confirmarse.",
            (AppointmentStatus.Confirmed, AppointmentStatus.Pending) =>
                "Una cita confirmada no puede regresar a «Pendiente».",
            _ =>
                $"No se permite cambiar una cita de «{from.ToDisplayName()}» a «{to.ToDisplayName()}»."
        };
    }
}
