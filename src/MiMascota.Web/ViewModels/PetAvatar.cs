using System.Globalization;
using System.Text;
using MiMascota.Web.Models;

namespace MiMascota.Web.ViewModels;

/// <summary>
/// Picks the image used as a pet's round avatar: the uploaded photo when there is one,
/// otherwise an illustration chosen from its species and breed (wwwroot/img/mascotas/avatar).
/// </summary>
public static class PetAvatar
{
    private const string Folder = "/img/mascotas/avatar/";
    public const string Fallback = Folder + "perro-pata.webp";

    /// <summary>The pet's photo if it has one, otherwise its illustration.</summary>
    public static string ImageFor(Pet? pet) =>
        string.IsNullOrWhiteSpace(pet?.PhotoPath) ? For(pet) : pet.PhotoPath;

    /// <summary>The illustration for the pet's species and breed (ignores the photo).</summary>
    public static string For(Pet? pet) => For(pet?.Species, pet?.Breed);

    public static string For(string? species, string? breed)
    {
        var s = Normalize(species);
        var b = Normalize(breed);

        var file = s switch
        {
            _ when s.StartsWith("perr") || s.StartsWith("canin") => b switch
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
            _ when IsBird(s) =>
                IsParakeet(s) || IsParakeet(b) ? "perico" : "cotorro",
            _ when s.StartsWith("huron") => "huron",
            _ when s.StartsWith("tortug") => "tortuga",
            _ when s.StartsWith("iguan") || s.StartsWith("reptil") || s.StartsWith("lagart") || s.StartsWith("gecko") => "iguana",
            _ => null
        };

        return file is null ? Fallback : Folder + file + ".webp";
    }

    private static bool IsBird(string s) =>
        s == "ave" || s == "aves" || s.StartsWith("ave ") || s.StartsWith("pajar")
        || s.StartsWith("cotorr") || s.StartsWith("loro") || s.StartsWith("canari") || s.StartsWith("cacatu")
        || IsParakeet(s);

    private static bool IsParakeet(string text) =>
        text.Contains("perico") || text.Contains("periquit") || text.Contains("australian");

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
