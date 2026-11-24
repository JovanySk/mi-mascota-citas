using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;
using MiMascota.Web.Models;

namespace MiMascota.Web.Controllers;

/// <summary>Simple CRUD for pets.</summary>
public class PetsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(int? ownerId)
    {
        var query = db.Pets.Include(p => p.Owner).AsNoTracking();

        if (ownerId is not null)
        {
            query = query.Where(p => p.OwnerId == ownerId);
            ViewData["OwnerName"] = await db.Owners.Where(o => o.Id == ownerId).Select(o => o.Name).FirstOrDefaultAsync();
        }

        return View(await query.OrderBy(p => p.Name).ToListAsync());
    }

    public async Task<IActionResult> Create(int? ownerId)
    {
        await LoadOwnersAsync();
        return View("Form", new Pet { OwnerId = ownerId ?? 0 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Species,Breed,OwnerId")] Pet pet)
    {
        if (!ModelState.IsValid)
        {
            await LoadOwnersAsync();
            return View("Form", pet);
        }

        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se registró a {pet.Name}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var pet = await db.Pets.FindAsync(id);
        if (pet is null)
        {
            return NotFound();
        }

        await LoadOwnersAsync();
        return View("Form", pet);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Species,Breed,OwnerId")] Pet pet)
    {
        if (id != pet.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await LoadOwnersAsync();
            return View("Form", pet);
        }

        db.Pets.Update(pet);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se actualizaron los datos de {pet.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var pet = await db.Pets.FindAsync(id);
        if (pet is null)
        {
            return NotFound();
        }

        if (await db.Appointments.AnyAsync(a => a.PetId == id))
        {
            TempData["Error"] = $"No se puede eliminar a {pet.Name} porque tiene citas en su historial.";
            return RedirectToAction(nameof(Index));
        }

        db.Pets.Remove(pet);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Se eliminó a {pet.Name}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadOwnersAsync()
    {
        var owners = await db.Owners.AsNoTracking().OrderBy(o => o.Name).ToListAsync();
        ViewData["Owners"] = new SelectList(owners, nameof(Owner.Id), nameof(Owner.Name));
    }
}
