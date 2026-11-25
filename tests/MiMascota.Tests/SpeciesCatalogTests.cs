using MiMascota.Web.ViewModels;

namespace MiMascota.Tests;

public class SpeciesCatalogTests
{
    [Theory]
    [InlineData("Ave", null, "Ave")]
    [InlineData("Tortuga", "ignorado", "Tortuga")]
    [InlineData("Otro", "  Erizo  ", "Erizo")]
    public void Resolves_the_species_to_store(string choice, string? other, string expected)
    {
        var (species, error) = SpeciesCatalog.Resolve(choice, other);

        Assert.Null(error);
        Assert.Equal(expected, species);
    }

    [Fact]
    public void Otro_without_text_asks_for_the_species()
    {
        var (_, error) = SpeciesCatalog.Resolve("Otro", " ");

        Assert.Contains("Escribe qué especie es", error);
    }

    [Theory]
    [InlineData("perro", "Perro")]
    [InlineData("HAMSTER", "Hámster")]
    [InlineData("huron", "Hurón")]
    [InlineData("Erizo", null)]
    [InlineData("Otro", null)]
    public void Matches_stored_species_with_the_chips(string stored, string? expected)
    {
        Assert.Equal(expected, SpeciesCatalog.Match(stored));
    }

    [Fact]
    public void Every_chip_except_otro_has_its_own_avatar()
    {
        var images = SpeciesCatalog.Options.Where(o => o.Value != SpeciesCatalog.Other).Select(o => o.Image).ToList();

        Assert.DoesNotContain(PetAvatar.Fallback, images.Skip(1)); // Perro uses perro-pata, which is also the fallback
        Assert.Equal(images.Count, images.Distinct().Count());
    }
}
