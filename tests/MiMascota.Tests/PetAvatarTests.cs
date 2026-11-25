using MiMascota.Web.ViewModels;

namespace MiMascota.Tests;

public class PetAvatarTests
{
    [Theory]
    [InlineData("Perro", "Husky siberiano", "husky")]
    [InlineData("Perro", "Bulldog francés", "frenchie-cono")]
    [InlineData("perro", "Salchicha", "salchicha-enfermera")]
    [InlineData("Perro", "Jack Russell", "vet-perro")]
    [InlineData("Perro", "Golden retriever", "perro-pata")]
    [InlineData("Gato", "Persa blanco", "vet-gato")]
    [InlineData("Gato", "British shorthair", "gato-asomado")]
    [InlineData("Cuyo", null, "cuyo")]
    [InlineData("Hámster", "Hámster sirio", "hamster")]
    [InlineData("HAMSTER", null, "hamster")]
    [InlineData("Conejo", "Belier", "conejo")]
    public void Picks_the_image_by_species_and_breed(string species, string? breed, string expected)
    {
        Assert.Equal($"/img/mascotas/avatar/{expected}.webp", PetAvatar.For(species, breed));
    }

    [Theory]
    [InlineData("Ave")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_species_use_the_fallback(string? species)
    {
        Assert.Equal(PetAvatar.Fallback, PetAvatar.For(species, null));
    }
}
