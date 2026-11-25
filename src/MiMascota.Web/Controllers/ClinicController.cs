using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Options;
using MiMascota.Web.Services;
using MiMascota.Web.ViewModels;

namespace MiMascota.Web.Controllers;

/// <summary>
/// The clinic's information pages linked from the footer (about, hours, directions,
/// help) plus the contact form and the staff inbox for its messages.
/// </summary>
public class ClinicController(
    AppDbContext db,
    IClinicHours hours,
    IOptions<ClinicOptions> clinicOptions,
    TimeProvider time) : Controller
{
    [HttpGet("clinica/nosotros")]
    public IActionResult About() => View();

    [HttpGet("clinica/horarios")]
    public IActionResult Hours()
    {
        ViewData["Status"] = hours.GetStatus();
        ViewData["SlotMinutes"] = clinicOptions.Value.SlotMinutes;
        return View(hours.Week());
    }

    [HttpGet("clinica/como-llegar")]
    public IActionResult Directions()
    {
        ViewData["Status"] = hours.GetStatus();
        return View(hours.Week());
    }

    [HttpGet("ayuda/preguntas")]
    public IActionResult Faq() => View(clinicOptions.Value);

    [HttpGet("ayuda/terminos")]
    public IActionResult Terms() => View();

    [HttpGet("ayuda/privacidad")]
    public IActionResult Privacy() => View();

    [HttpGet("contacto")]
    public IActionResult Contact() => View(new ContactFormViewModel());

    [HttpPost("contacto")]
    [ValidateAntiForgeryToken]
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

    [HttpGet("contacto/mensajes")]
    public async Task<IActionResult> Messages()
    {
        var messages = await db.ContactMessages.AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync();
        return View(new ContactMessagesViewModel { Messages = messages });
    }

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
