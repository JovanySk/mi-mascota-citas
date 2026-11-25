using System.Globalization;
using System.Text;
using MiMascota.Web.Models;

namespace MiMascota.Web.ViewModels;

/// <summary>
/// Picks the illustration used as a pet's round avatar, from its species and breed.
/// The images live in wwwroot/img/mascotas/avatar.
/// </summary>
public static class PetAvatar
{
    private const string Folder = "/img/mascotas/avatar/";
    public const string Fallback = Folder + "perro-pata.webp";

    public static string For(Pet? pet) => For(pet?.Species, pet?.Breed);

    public static string For(string? species, string? breed)
    {
        var s = Normalize(species);
        var b = Normalize(breed);

        var file = s switch
        {
            _ when s.StartsWith("perr") || s.StartsWith("can") => b switch
            {
                _ when b.Contains("husky") => "husky",
                _ when b.Contains("bulldog") || b.Contains("frances") || b.Contains("pug") => "frenchie-cono",
                _ when b.Contains("salchicha") || b.Contains("dachshund") => "salchicha-enfermera",
                _ when b.Contains("jack") || b.Contains("terrier") || b.Contains("chihuahua") || b.Contains("beagle") => "vet-perro",
                _ => "perro-pata"
            },
            _ when s.StartsWith("gat") || s.StartsWith("felin") =>
                b.Contains("persa") || b.Contains("blanc") || b.Contains("angora") ? "vet-gato" : "gato-asomado",
            _ when s.StartsWith("cuy") || s.StartsWith("cobay") => "cuyo",
            _ when s.StartsWith("hamst") => "hamster",
            _ when s.StartsWith("conej") => "conejo",
            _ => null
        };

        return file is null ? Fallback : Folder + file + ".webp";
    }

    /// <summary>Lower case without accents: "Hámster" → "hamster".</summary>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString();
    }
}
