using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Options;
using MiMascota.Web.Security;
using MiMascota.Web.Services;
using MiMascota.Web.ViewModels;

namespace MiMascota.Web.Controllers;

/// <summary>
/// The clinic's information pages linked from the footer (about, hours, directions,
/// help) plus the contact form, all public, and the staff inbox for its messages.
/// Public actions say [AllowAnonymous]; everything else falls back to "staff only".
/// </summary>
public class ClinicController(
    AppDbContext db,
    IClinicHours hours,
    IOptions<ClinicOptions> clinicOptions,
    TimeProvider time) : Controller
{
    [AllowAnonymous]
    [HttpGet("clinica/nosotros")]
    public IActionResult About() => View();

    [AllowAnonymous]
    [HttpGet("clinica/horarios")]
    public IActionResult Hours()
    {
        ViewData["Status"] = hours.GetStatus();
        ViewData["SlotMinutes"] = clinicOptions.Value.SlotMinutes;
        return View(hours.Week());
    }

    [AllowAnonymous]
    [HttpGet("clinica/como-llegar")]
    public IActionResult Directions()
    {
        ViewData["Status"] = hours.GetStatus();
        return View(hours.Week());
    }

    [AllowAnonymous]
    [HttpGet("ayuda/preguntas")]
    public IActionResult Faq() => View(clinicOptions.Value);

    [AllowAnonymous]
    [HttpGet("ayuda/terminos")]
    public IActionResult Terms() => View();

    [AllowAnonymous]
    [HttpGet("ayuda/privacidad")]
    public IActionResult Privacy() => View();

    // GET /contacto?servicio=Baño y estética  → message prefilled to ask for that service
    // GET /contacto?motivo=cita                → message prefilled to ask for an appointment
    [AllowAnonymous]
    [HttpGet("contacto")]
    public async Task<IActionResult> Contact(string? servicio, string? motivo)
    {
        var model = new ContactFormViewModel();

        if (!string.IsNullOrWhiteSpace(servicio))
        {
            // Only a real service name is copied into the form (never arbitrary text from the URL).
            var wanted = servicio.Trim();
            var names = await db.VetServices.AsNoTracking().Select(s => s.Name).ToListAsync();
            var name = names.FirstOrDefault(n => string.Equals(n.Trim(), wanted, StringComparison.CurrentCultureIgnoreCase));
            model.Message = name is null ? ContactFormViewModel.AppointmentRequest() : ContactFormViewModel.AppointmentRequest(name);
            ViewData["Service"] = name;
        }
        else if (string.Equals(motivo, "cita", StringComparison.OrdinalIgnoreCase))
        {
            model.Message = ContactFormViewModel.AppointmentRequest();
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpPost("contacto")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimits.Contact)]
    public async Task<IActionResult> Contact(ContactFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        db.ContactMessages.Add(new ContactMessage
        {
            Name = model.Name.Trim(),
            Contact = model.Contact.Trim(),
            Message = model.Message.Trim(),
            CreatedAt = time.GetLocalNow().DateTime
        });
        await db.SaveChangesAsync();

        // Post/Redirect/Get: a refresh does not send the message twice.
        TempData["Success"] = $"¡Gracias, {model.Name.Trim()}! Recibimos tu mensaje y te responderemos pronto.";
        return RedirectToAction(nameof(Contact));
    }

    [Authorize(Roles = Roles.AnyStaff)]
    [HttpGet("contacto/mensajes")]
    public async Task<IActionResult> Messages()
    {
        var messages = await db.ContactMessages.AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync();
        return View(new ContactMessagesViewModel { Messages = messages });
    }

    [Authorize(Roles = Roles.AnyStaff)]
    [HttpPost("contacto/mensajes/{id:int}/leido")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var message = await db.ContactMessages.FindAsync(id);
        if (message is null)
        {
            TempData["Error"] = "Ese mensaje ya no existe.";
        }
        else if (!message.IsRead)
        {
            message.IsRead = true;
            await db.SaveChangesAsync();
            TempData["Success"] = $"Mensaje de {message.Name} marcado como leído.";
        }

        return RedirectToAction(nameof(Messages));
    }
}
