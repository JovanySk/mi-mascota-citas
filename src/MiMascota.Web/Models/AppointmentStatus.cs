namespace MiMascota.Web.Models;

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    Attended,
    Cancelled
}

/// <summary>Display helpers so the UI can show statuses in Spanish.</summary>
public static class AppointmentStatusExtensions
{
    public static string ToDisplayName(this AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Pendiente",
        AppointmentStatus.Confirmed => "Confirmada",
        AppointmentStatus.Attended => "Atendida",
        AppointmentStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };

    /// <summary>CSS class of the status badge (colors defined in site.css).</summary>
    public static string ToBadgeClass(this AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "is-pending",
        AppointmentStatus.Confirmed => "is-confirmed",
        AppointmentStatus.Attended => "is-attended",
        AppointmentStatus.Cancelled => "is-cancelled",
        _ => string.Empty
    };

    /// <summary>Text for the button that moves an appointment INTO this status.</summary>
    public static string ToActionLabel(this AppointmentStatus status) => status switch
    {
        AppointmentStatus.Confirmed => "Confirmar",
        AppointmentStatus.Attended => "Marcar atendida",
        AppointmentStatus.Cancelled => "Cancelar",
        _ => status.ToDisplayName()
    };
}
