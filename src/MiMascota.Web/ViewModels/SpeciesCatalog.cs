using System.Globalization;
using System.Text;

namespace MiMascota.Web.ViewModels;

/// <summary>One choice of the species picker (radio "chip" with the species' avatar).</summary>
public record SpeciesOption(string Value, string Label, string? Hint)
{
    /// <summary>Illustration shown on the chip.</summary>
    public string Image => Value == SpeciesCatalog.Other ? "/img/paw.svg" : PetAvatar.For(Value, null);
}

/// <summary>
/// The species offered in the pet forms. Species is still stored as free text,
/// so "Otro" lets staff write any species that is not in the list.
/// </summary>
public static class SpeciesCatalog
{
    public const string Other = "Otro";

    public static readonly IReadOnlyList<SpeciesOption> Options =
    [
        new("Perro", "Perro", null),
        new("Gato", "Gato", null),
        new("Ave", "Ave", "cotorro, perico…"),
        new("Cuyo", "Cuyo", null),
        new("Hámster", "Hámster", null),
        new("Conejo", "Conejo", null),
        new("Hurón", "Hurón", null),
        new("Tortuga", "Tortuga", null),
        new("Iguana", "Iguana / reptil", null),
        new(Other, "Otro", "escríbelo"),
    ];

    /// <summary>The catalog value matching a stored species ("perro" → "Perro"), or null if it is a custom one.</summary>
    public static string? Match(string? species)
    {
        var normalized = Normalize(species);
        if (normalized.Length == 0) return null;
        return Options.FirstOrDefault(o => o.Value != Other && Normalize(o.Value) == normalized)?.Value;
    }

    /// <summary>
    /// Turns the picker's answer into the species to store: the chosen chip, or the
    /// custom text when "Otro" is chosen. Returns a Spanish error when "Otro" has no text.
    /// </summary>
    public static (string? Species, string? Error) Resolve(string? choice, string? otherText)
    {
        if (string.IsNullOrWhiteSpace(choice))
        {
            return (null, null); // [Required] on Pet.Species reports it.
        }

        if (!string.Equals(choice.Trim(), Other, StringComparison.OrdinalIgnoreCase))
        {
            return (choice.Trim(), null);
        }

        var custom = otherText?.Trim();
        return string.IsNullOrEmpty(custom)
            ? (choice.Trim(), "Escribe qué especie es (por ejemplo: erizo, pez, gallina).")
            : (custom, null);
    }

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
