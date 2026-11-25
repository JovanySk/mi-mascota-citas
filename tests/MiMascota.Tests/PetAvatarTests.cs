using MiMascota.Web.Models;
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
    [InlineData("Ave", null, "cotorro")]
    [InlineData("Ave", "Cotorro cabeza amarilla", "cotorro")]
    [InlineData("Cotorro", null, "cotorro")]
    [InlineData("Loro", "Amazona", "cotorro")]
    [InlineData("Canario", null, "cotorro")]
    [InlineData("Ave", "Perico australiano", "perico")]
    [InlineData("Ave", "Periquito", "perico")]
    [InlineData("Perico", null, "perico")]
    [InlineData("Periquito australiano", null, "perico")]
    [InlineData("Hurón", "Hurón canela", "huron")]
    [InlineData("huron", null, "huron")]
    [InlineData("Tortuga", "Orejas rojas", "tortuga")]
    [InlineData("Iguana", "Iguana verde", "iguana")]
    [InlineData("Reptil", null, "iguana")]
    [InlineData("Lagartija", null, "iguana")]
    [InlineData("Gecko", "Leopardo", "iguana")]
    public void Exotic_species_have_their_own_avatar(string species, string? breed, string expected)
    {
        Assert.Equal($"/img/mascotas/avatar/{expected}.webp", PetAvatar.For(species, breed));
    }

    [Theory]
    [InlineData("Avestruz")]
    [InlineData("Erizo")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_species_use_the_fallback(string? species)
    {
        Assert.Equal(PetAvatar.Fallback, PetAvatar.For(species, null));
    }

    [Fact]
    public void Uploaded_photo_wins_over_the_illustration()
    {
        var pet = new Pet { Species = "Ave", PhotoPath = "/uploads/mascotas/abc.jpg" };

        Assert.Equal("/uploads/mascotas/abc.jpg", PetAvatar.ImageFor(pet));
        Assert.Equal("/img/mascotas/avatar/cotorro.webp", PetAvatar.For(pet));
    }

    [Fact]
    public void Without_photo_the_illustration_is_used()
    {
        Assert.Equal("/img/mascotas/avatar/tortuga.webp", PetAvatar.ImageFor(new Pet { Species = "Tortuga" }));
        Assert.Equal(PetAvatar.Fallback, PetAvatar.ImageFor(null));
    }
}
