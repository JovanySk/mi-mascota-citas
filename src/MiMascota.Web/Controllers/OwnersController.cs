using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;
using MiMascota.Web.Models;

namespace MiMascota.Web.Controllers;

/// <summary>Simple CRUD for pet owners (no business rules, so it uses the DbContext directly).</summary>
public class OwnersController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var query = db.Owners.Include(o => o.Pets).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(o => o.Name.Contains(search) || o.Phone.Contains(search));
        }

        ViewData["Search"] = search;
        return View(await query.OrderBy(o => o.Name).ToListAsync());
    }

    public IActionResult Create() => View("Form", new Owner());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Phone,Email")] Owner owner)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", owner);
        }

        db.Owners.Add(owner);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se registró a {owner.Name}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var owner = await db.Owners.FindAsync(id);
        return owner is null ? NotFound() : View("Form", owner);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Phone,Email")] Owner owner)
    {
        if (id != owner.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View("Form", owner);
        }

        db.Owners.Update(owner);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se actualizaron los datos de {owner.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var owner = await db.Owners.Include(o => o.Pets).FirstOrDefaultAsync(o => o.Id == id);
        if (owner is null)
        {
            return NotFound();
        }

        if (owner.Pets.Count > 0)
        {
            TempData["Error"] = $"No se puede eliminar a {owner.Name} porque tiene {owner.Pets.Count} mascota(s) registrada(s).";
            return RedirectToAction(nameof(Index));
        }

        db.Owners.Remove(owner);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se eliminó a {owner.Name}.";
        return RedirectToAction(nameof(Index));
    }
}
