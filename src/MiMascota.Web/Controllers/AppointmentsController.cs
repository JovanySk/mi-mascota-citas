using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Services;
using MiMascota.Web.ViewModels;

namespace MiMascota.Web.Controllers;

/// <summary>
/// Thin controller: it only translates HTTP requests into service calls.
/// Booking rules live in AppointmentService and AvailabilityService.
/// </summary>
public class AppointmentsController(
    IAppointmentService appointments,
    IAvailabilityService availability,
    AppDbContext db,
    TimeProvider time) : Controller
{
    // GET /Appointments?date=2026-11-25&status=Pending
    public async Task<IActionResult> Index(DateOnly? date, AppointmentStatus? status)
    {
        var model = new AppointmentListViewModel
        {
            Date = date,
            Status = status,
            Appointments = await appointments.ListAsync(date, status)
        };
        return View(model);
    }

    // GET /Appointments/Create?date=2026-11-25&serviceId=1&startTime=11:00
    // (startTime only preselects a slot; the server checks it again on POST)
    public async Task<IActionResult> Create(DateOnly? date, int? serviceId, int? petId, TimeOnly? startTime)
    {
        var model = new AppointmentFormViewModel
        {
            Date = date ?? DateOnly.FromDateTime(time.GetLocalNow().DateTime),
            ServiceId = serviceId,
            PetId = petId,
            StartTime = startTime
        };
        await FillListsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await FillListsAsync(model);
            return View(model);
        }

        var start = model.Date!.Value.ToDateTime(model.StartTime!.Value);
        var result = await appointments.CreateAsync(
            new NewAppointment(model.PetId!.Value, model.ServiceId!.Value, start, model.Notes));

        if (result.IsFailure)
        {
            // Business rule broken (e.g. overlapping slot): show the message in the form.
            ModelState.AddModelError(string.Empty, result.Error!);
            await FillListsAsync(model);
            return View(model);
        }

        TempData["Success"] = $"Cita agendada para el {start:dddd d 'de' MMMM 'a las' HH:mm}. Queda como «Pendiente» hasta confirmarla.";
        return RedirectToAction(nameof(Index), new { date = model.Date.Value.ToString("yyyy-MM-dd") });
    }

    // GET /Appointments/Availability?date=2026-11-25&serviceId=1  → JSON used by the form (AJAX)
    [HttpGet]
    public async Task<IActionResult> Availability(DateOnly? date, int? serviceId)
    {
        if (date is null || serviceId is null)
        {
            return BadRequest(new { message = "Indica la fecha y el servicio." });
        }

        var result = await availability.GetAvailableSlotsAsync(date.Value, serviceId.Value);
        var slots = result.Value ?? [];

        return Json(new
        {
            date = date.Value.ToString("yyyy-MM-dd"),
            serviceId,
            available = slots.Count > 0,
            message = result.IsFailure
                ? result.Error
                : slots.Count == 0 ? "No quedan horarios disponibles para este día." : null,
            slots = slots.Select(s => new { time = s.Start.ToString("HH:mm"), label = s.Label })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, AppointmentStatus status, string? returnUrl)
    {
        var result = await appointments.ChangeStatusAsync(id, status);

        if (result.IsSuccess)
        {
            TempData["Success"] = $"La cita ahora está «{status.ToDisplayName()}».";
        }
        else
        {
            TempData["Error"] = result.Error;
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    private async Task FillListsAsync(AppointmentFormViewModel model)
    {
        var pets = await db.Pets.Include(p => p.Owner).AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var services = await db.VetServices.AsNoTracking().OrderBy(s => s.Name).ToListAsync();

        model.Pets = pets.Select(p => new SelectListItem($"{p.Name} ({p.Species}) — {p.Owner?.Name}", p.Id.ToString()));
        model.Services = services.Select(s => new SelectListItem($"{s.Name} · {s.DurationMinutes} min · {s.Price:C0}", s.Id.ToString()));
    }
}
