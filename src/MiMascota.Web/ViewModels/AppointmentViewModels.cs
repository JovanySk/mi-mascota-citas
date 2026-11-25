using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using MiMascota.Web.Models;

namespace MiMascota.Web.ViewModels;

/// <summary>Form used to book a new appointment.</summary>
public class AppointmentFormViewModel
{
    [Display(Name = "Mascota")]
    [Required(ErrorMessage = "Selecciona una mascota.")]
    public int? PetId { get; set; }

    [Display(Name = "Servicio")]
    [Required(ErrorMessage = "Selecciona un servicio.")]
    public int? ServiceId { get; set; }

    [Display(Name = "Fecha")]
    [Required(ErrorMessage = "Selecciona una fecha.")]
    public DateOnly? Date { get; set; }

    [Display(Name = "Horario")]
    [Required(ErrorMessage = "Selecciona uno de los horarios disponibles.")]
    public TimeOnly? StartTime { get; set; }

    [Display(Name = "Notas")]
    [StringLength(500, ErrorMessage = "Las notas no pueden tener más de {1} caracteres.")]
    public string? Notes { get; set; }

    [ValidateNever]
    public IEnumerable<SelectListItem> Pets { get; set; } = [];

    [ValidateNever]
    public IEnumerable<SelectListItem> Services { get; set; } = [];
}

/// <summary>Appointment list with its filters.</summary>
public class AppointmentListViewModel
{
    public DateOnly? Date { get; set; }
    public AppointmentStatus? Status { get; set; }
    public IReadOnlyList<Appointment> Appointments { get; set; } = [];
}

/// <summary>Home page: landing sections fed with the real agenda of the day.</summary>
public class DashboardViewModel
{
    public DateOnly Today { get; set; }
    public IReadOnlyList<Appointment> TodayAppointments { get; set; } = [];

    public int CountFor(AppointmentStatus status) => TodayAppointments.Count(a => a.Status == status);

    /// <summary>"9:00–18:00"</summary>
    public string HoursLabel { get; set; } = string.Empty;

    /// <summary>"Lun a Sáb"</summary>
    public string OpenDaysLabel { get; set; } = string.Empty;

    public int SlotMinutes { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }

    /// <summary>Service and day offered in the "Nueva cita rápida" card (null when there are no services).</summary>
    public VetService? QuickService { get; set; }
    public DateOnly QuickDate { get; set; }

    /// <summary>The grooming service, if the catalog has one (bath banner).</summary>
    public VetService? BathService { get; set; }
}
