using MiMascota.Web.Models;

namespace MiMascota.Web.ViewModels;

/// <summary>Model of the _SpeciesPicker partial.</summary>
/// <param name="Selected">Species currently stored (or posted) for the pet.</param>
/// <param name="IdPrefix">Prefix for the element ids, so the picker can appear twice in a page.</param>
public record SpeciesPickerModel(string? Selected, string IdPrefix = "species");

/// <summary>Fields posted by the "Registrar mascota nueva" modal of the booking form.</summary>
public class QuickPetForm
{
    public string? Name { get; set; }
    public string? Species { get; set; }
    public string? SpeciesOther { get; set; }
    public string? Breed { get; set; }
    public int OwnerId { get; set; }

    /// <summary>True when the owner is registered in the same step (name + phone).</summary>
    public bool NewOwner { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnerPhone { get; set; }

    public IFormFile? Photo { get; set; }
}

public static class PetLabels
{
    /// <summary>Text of a pet in the booking form's drop-down: "Paco (Ave) — Ana Ruiz".</summary>
    public static string ForSelect(Pet pet) => $"{pet.Name} ({pet.Species}) — {pet.Owner?.Name}";
}

/// <summary>Model of the _PhotoField partial (photo input with live round preview).</summary>
/// <param name="CurrentPhoto">Photo already saved for the pet (edit form), or null.</param>
/// <param name="Illustration">Avatar shown while there is no photo.</param>
/// <param name="IdPrefix">Prefix for the element ids.</param>
public record PhotoFieldModel(string? CurrentPhoto, string Illustration, string IdPrefix = "photo");
