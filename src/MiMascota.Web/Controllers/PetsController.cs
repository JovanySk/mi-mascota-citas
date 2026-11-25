using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Services;
using MiMascota.Web.ViewModels;

namespace MiMascota.Web.Controllers;

/// <summary>CRUD for pets, their photo, and the quick registration used by the booking form.</summary>
public class PetsController(AppDbContext db, IPetPhotoStorage photos) : Controller
{
    private const string PhotoField = "Photo";

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
    public async Task<IActionResult> Create([Bind("Name,Species,Breed,OwnerId")] Pet pet, string? speciesOther, IFormFile? photo)
    {
        ApplySpecies(pet, speciesOther);
        ValidatePhoto(photo);

        if (!ModelState.IsValid)
        {
            await LoadOwnersAsync();
            return View("Form", pet);
        }

        if (photo is not null)
        {
            pet.PhotoPath = (await photos.SaveAsync(photo)).Value;
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
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Species,Breed,OwnerId")] Pet pet,
        string? speciesOther, IFormFile? photo, bool removePhoto)
    {
        if (id != pet.Id)
        {
            return BadRequest();
        }

        var saved = await db.Pets.FindAsync(id);
        if (saved is null)
        {
            return NotFound();
        }

        ApplySpecies(pet, speciesOther);
        ValidatePhoto(photo);

        if (!ModelState.IsValid)
        {
            pet.PhotoPath = saved.PhotoPath; // keep showing the current photo
            await LoadOwnersAsync();
            return View("Form", pet);
        }

        saved.Name = pet.Name;
        saved.Species = pet.Species;
        saved.Breed = pet.Breed;
        saved.OwnerId = pet.OwnerId;

        var oldPhoto = saved.PhotoPath;
        if (photo is not null)
        {
            saved.PhotoPath = (await photos.SaveAsync(photo)).Value;
        }
        else if (removePhoto)
        {
            saved.PhotoPath = null;
        }

        await db.SaveChangesAsync();

        if (oldPhoto != saved.PhotoPath)
        {
            photos.Delete(oldPhoto); // only once the new path is stored
        }

        TempData["Success"] = $"Se actualizaron los datos de {saved.Name}.";
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
        photos.Delete(pet.PhotoPath);
        TempData["Success"] = $"Se eliminó a {pet.Name}.";
        return RedirectToAction(nameof(Index));
    }

    // POST /Pets/QuickCreate (multipart, AJAX from the booking form's modal)
    // → 200 { id, name, label, avatar, message } or 400 { errors: { field: [messages] } }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickCreate(QuickPetForm form)
    {
        var errors = new Dictionary<string, List<string>>();
        void AddError(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
            list.Add(message);
        }

        var (species, speciesError) = SpeciesCatalog.Resolve(form.Species, form.SpeciesOther);
        if (speciesError is not null) AddError(nameof(QuickPetForm.SpeciesOther), speciesError);

        var pet = new Pet
        {
            Name = form.Name?.Trim() ?? string.Empty,
            Species = species ?? string.Empty,
            Breed = string.IsNullOrWhiteSpace(form.Breed) ? null : form.Breed.Trim(),
            OwnerId = form.NewOwner ? 0 : form.OwnerId
        };

        // Same data annotations as the full pet form.
        foreach (var error in ValidateObject(pet))
        {
            foreach (var member in error.MemberNames)
            {
                if (form.NewOwner && member == nameof(Pet.OwnerId)) continue;
                AddError(member, error.ErrorMessage!);
            }
        }

        Owner? newOwner = null;
        if (form.NewOwner)
        {
            newOwner = new Owner { Name = form.OwnerName?.Trim() ?? string.Empty, Phone = form.OwnerPhone?.Trim() ?? string.Empty };
            foreach (var error in ValidateObject(newOwner))
            {
                foreach (var member in error.MemberNames)
                {
                    var field = member == nameof(Owner.Name) ? nameof(QuickPetForm.OwnerName) : nameof(QuickPetForm.OwnerPhone);
                    AddError(field, error.ErrorMessage!);
                }
            }
        }
        else if (pet.OwnerId > 0 && !await db.Owners.AnyAsync(o => o.Id == pet.OwnerId))
        {
            AddError(nameof(Pet.OwnerId), "El dueño seleccionado no existe.");
        }

        if (form.Photo is not null)
        {
            var photoCheck = photos.Validate(form.Photo);
            if (photoCheck.IsFailure) AddError(PhotoField, photoCheck.Error!);
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { errors });
        }

        pet.Owner = newOwner ?? await db.Owners.FindAsync(pet.OwnerId);

        if (form.Photo is not null)
        {
            pet.PhotoPath = (await photos.SaveAsync(form.Photo)).Value;
        }

        db.Pets.Add(pet);
        await db.SaveChangesAsync();

        return Json(new
        {
            id = pet.Id,
            name = pet.Name,
            label = PetLabels.ForSelect(pet),
            avatar = PetAvatar.ImageFor(pet),
            message = newOwner is null
                ? $"Se registró a {pet.Name}. Ya puedes elegir su horario."
                : $"Se registró a {pet.Name} y a su dueño, {newOwner.Name}. Ya puedes elegir su horario."
        });
    }

    /// <summary>Uses the custom text when the "Otro" chip is chosen (see SpeciesCatalog.Resolve).</summary>
    private void ApplySpecies(Pet pet, string? speciesOther)
    {
        var (species, error) = SpeciesCatalog.Resolve(pet.Species, speciesOther);
        if (error is not null)
        {
            ModelState.AddModelError("SpeciesOther", error);
            return;
        }

        if (species is not null && species != pet.Species)
        {
            pet.Species = species;
            ModelState.Remove(nameof(Pet.Species));
            if (species.Length > 40)
            {
                ModelState.AddModelError(nameof(Pet.Species), "La especie no puede tener más de 40 caracteres.");
            }
        }
    }

    private void ValidatePhoto(IFormFile? photo)
    {
        if (photo is null) return;
        var result = photos.Validate(photo);
        if (result.IsFailure)
        {
            ModelState.AddModelError(PhotoField, result.Error!);
        }
    }

    private static List<ValidationResult> ValidateObject(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private async Task LoadOwnersAsync()
    {
        var owners = await db.Owners.AsNoTracking().OrderBy(o => o.Name).ToListAsync();
        ViewData["Owners"] = new SelectList(owners, nameof(Owner.Id), nameof(Owner.Name));
    }
}
