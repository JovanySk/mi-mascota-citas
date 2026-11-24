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

/// <summary>Home page summary.</summary>
public class DashboardViewModel
{
    public DateOnly Today { get; set; }
    public IReadOnlyList<Appointment> TodayAppointments { get; set; } = [];

    public int CountFor(AppointmentStatus status) => TodayAppointments.Count(a => a.Status == status);
}
