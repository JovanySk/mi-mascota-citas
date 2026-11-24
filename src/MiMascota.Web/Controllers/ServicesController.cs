using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;
using MiMascota.Web.Models;

namespace MiMascota.Web.Controllers;

/// <summary>Simple CRUD for the clinic's services (consultation, vaccination...).</summary>
public class ServicesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var services = await db.VetServices.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return View(services);
    }

    public IActionResult Create() => View("Form", new VetService());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,DurationMinutes,Price")] VetService service)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", service);
        }

        db.VetServices.Add(service);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se agregó el servicio «{service.Name}».";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var service = await db.VetServices.FindAsync(id);
        return service is null ? NotFound() : View("Form", service);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,DurationMinutes,Price")] VetService service)
    {
        if (id != service.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View("Form", service);
        }

        db.VetServices.Update(service);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se actualizó el servicio «{service.Name}». Las citas ya agendadas conservan su horario.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await db.VetServices.FindAsync(id);
        if (service is null)
        {
            return NotFound();
        }

        if (await db.Appointments.AnyAsync(a => a.VetServiceId == id))
        {
            TempData["Error"] = $"No se puede eliminar «{service.Name}» porque ya se usó en citas.";
            return RedirectToAction(nameof(Index));
        }

        db.VetServices.Remove(service);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se eliminó el servicio «{service.Name}».";
        return RedirectToAction(nameof(Index));
    }
}
