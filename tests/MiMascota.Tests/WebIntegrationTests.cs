using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiMascota.Web.Data;
using MiMascota.Web.Models;

namespace MiMascota.Tests;

/// <summary>Runs the real app against a temporary SQLite file without demo data.</summary>
public sealed class MiMascotaWebFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"mimascota-tests-{Guid.NewGuid():N}.db");

    /// <summary>Uploaded photos go here instead of the real wwwroot.</summary>
    public string PhotosRoot { get; } = Path.Combine(Path.GetTempPath(), $"mimascota-tests-photos-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:SeedDemoData"] = "false",
            ["ConnectionStrings:Sqlite"] = $"Data Source={_databasePath}",
            ["PetPhotos:WebRootPath"] = PhotosRoot
        }));
    }

    /// <summary>Runs code against the app's database (for test setup).</summary>
    public T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { File.Delete(_databasePath); } catch (IOException) { /* best effort */ }
        try { if (Directory.Exists(PhotosRoot)) Directory.Delete(PhotosRoot, recursive: true); } catch (IOException) { /* best effort */ }
    }
}

public class WebIntegrationTests(MiMascotaWebFactory factory) : IClassFixture<MiMascotaWebFactory>
{
    /// <summary>An open day at least a week ahead, so "now" never interferes.</summary>
    private static DateOnly FutureOpenDay()
    {
        var day = DateOnly.FromDateTime(DateTime.Now).AddDays(7);
        return day.DayOfWeek == DayOfWeek.Sunday ? day.AddDays(1) : day;
    }

    private (int PetId, int ServiceId) CreatePetAndService(int durationMinutes) => factory.WithDb(db =>
    {
        var service = TestData.AddService(db, durationMinutes, $"Servicio {Guid.NewGuid():N}"[..20]);
        var pet = TestData.AddPet(db, "Luna");
        return (pet.Id, service.Id);
    });

    [Fact]
    public async Task Main_pages_render()
    {
        var client = factory.CreateClient();

        foreach (var url in new[] { "/", "/Appointments", "/Appointments/Create", "/Owners", "/Pets", "/Pets/Create", "/Services" })
        {
            var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} returned {response.StatusCode}");
        }
    }

    [Fact]
    public async Task Availability_endpoint_returns_free_slots_as_json()
    {
        var (_, serviceId) = CreatePetAndService(30);
        var day = FutureOpenDay();
        var client = factory.CreateClient();

        var json = await client.GetFromJsonAsync<JsonElement>(
            $"/Appointments/Availability?date={day:yyyy-MM-dd}&serviceId={serviceId}");

        Assert.True(json.GetProperty("available").GetBoolean());
        var slots = json.GetProperty("slots").EnumerateArray().ToList();
        Assert.NotEmpty(slots);
        Assert.Equal("09:00", slots[0].GetProperty("time").GetString());
        Assert.Equal("09:00 – 09:30", slots[0].GetProperty("label").GetString());
    }

    [Fact]
    public async Task Availability_endpoint_explains_when_the_clinic_is_closed()
    {
        var (_, serviceId) = CreatePetAndService(30);
        var sunday = FutureOpenDay();
        while (sunday.DayOfWeek != DayOfWeek.Sunday) sunday = sunday.AddDays(1);
        var client = factory.CreateClient();

        var json = await client.GetFromJsonAsync<JsonElement>(
            $"/Appointments/Availability?date={sunday:yyyy-MM-dd}&serviceId={serviceId}");

        Assert.False(json.GetProperty("available").GetBoolean());
        Assert.Contains("domingos", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Posting_an_overlapping_appointment_shows_the_error_in_the_form()
    {
        var (petId, serviceId) = CreatePetAndService(60);
        var day = FutureOpenDay();
        factory.WithDb(db =>
        {
            var pet = db.Pets.Find(petId)!;
            var service = db.VetServices.Find(serviceId)!;
            return TestData.AddAppointment(db, pet, service, day.At(12)); // 12:00–13:00
        });
        var client = factory.CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Appointments/Create");

        var response = await client.PostAsync("/Appointments/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["PetId"] = petId.ToString(),
            ["ServiceId"] = serviceId.ToString(),
            ["Date"] = day.ToString("yyyy-MM-dd"),
            ["StartTime"] = "12:30"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // form re-rendered, no redirect
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("ya está ocupado", html);
        Assert.Equal(1, factory.WithDb(db => db.Appointments.Count(a => a.PetId == petId)));
    }

    [Fact]
    public async Task Posting_a_free_slot_creates_the_appointment_and_redirects()
    {
        var (petId, serviceId) = CreatePetAndService(30);
        var day = FutureOpenDay().AddDays(1);
        if (day.DayOfWeek == DayOfWeek.Sunday) day = day.AddDays(1);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetAntiforgeryTokenAsync(client, "/Appointments/Create");

        var response = await client.PostAsync("/Appointments/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["PetId"] = petId.ToString(),
            ["ServiceId"] = serviceId.ToString(),
            ["Date"] = day.ToString("yyyy-MM-dd"),
            ["StartTime"] = "16:00",
            ["Notes"] = "Prueba de integración"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = factory.WithDb(db => db.Appointments.Single(a => a.PetId == petId));
        Assert.Equal(AppointmentStatus.Pending, saved.Status);
        Assert.Equal(day.At(16), saved.Start);
    }

    [Fact]
    public async Task Changing_status_without_antiforgery_token_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/Appointments/ChangeStatus/1",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["status"] = "Confirmed" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Quick_create_registers_a_pet_with_photo_for_an_existing_owner()
    {
        var ownerId = factory.WithDb(db => TestData.AddPet(db, "Mascota previa").OwnerId);
        var client = factory.CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Appointments/Create");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Paco"), "Name" },
            { new StringContent("Ave"), "Species" },
            { new StringContent("Cotorro"), "Breed" },
            { new StringContent("false"), "NewOwner" },
            { new StringContent(ownerId.ToString()), "OwnerId" },
            { new ByteArrayContent(PetPhotoStorageTests.RealImage()), "Photo", "paco.png" }
        };
        var response = await client.PostAsync("/Pets/QuickCreate", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = json.GetProperty("id").GetInt32();
        Assert.StartsWith("Paco (Ave) — ", json.GetProperty("label").GetString());

        var saved = factory.WithDb(db => db.Pets.Single(p => p.Id == id));
        Assert.Equal(ownerId, saved.OwnerId);
        Assert.Equal(saved.PhotoPath, json.GetProperty("avatar").GetString());
        Assert.StartsWith("/uploads/mascotas/", saved.PhotoPath);
        Assert.True(File.Exists(Path.Combine(factory.PhotosRoot, "uploads", "mascotas", Path.GetFileName(saved.PhotoPath!))));
    }

    [Fact]
    public async Task Quick_create_can_register_the_owner_too()
    {
        var client = factory.CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Appointments/Create");

        var response = await client.PostAsync("/Pets/QuickCreate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Manuelita",
            ["Species"] = "Otro",
            ["SpeciesOther"] = "Tortuga de tierra",
            ["NewOwner"] = "true",
            ["OwnerName"] = "Dueña Nueva Rápida",
            ["OwnerPhone"] = "55 1111 2222"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var saved = factory.WithDb(db => db.Pets.Include(p => p.Owner).Single(p => p.Id == id));
        Assert.Equal("Tortuga de tierra", saved.Species);
        Assert.Equal("Dueña Nueva Rápida", saved.Owner!.Name);
        Assert.Null(saved.PhotoPath);
    }

    [Fact]
    public async Task Quick_create_returns_spanish_validation_errors_as_json()
    {
        var client = factory.CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Appointments/Create");
        var before = factory.WithDb(db => db.Pets.Count());

        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(""), "Name" },
            { new StringContent("Otro"), "Species" },
            { new StringContent("true"), "NewOwner" },
            { new StringContent("Sin teléfono"), "OwnerName" },
            { new ByteArrayContent("no soy una foto"u8.ToArray()), "Photo", "foto.jpg" }
        };
        var response = await client.PostAsync("/Pets/QuickCreate", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains("obligatorio", errors.GetProperty("Name")[0].GetString());
        Assert.Contains("Escribe qué especie es", errors.GetProperty("SpeciesOther")[0].GetString());
        Assert.Contains("teléfono", errors.GetProperty("OwnerPhone")[0].GetString());
        Assert.Contains("no es una imagen válida", errors.GetProperty("Photo")[0].GetString());
        Assert.False(errors.TryGetProperty("OwnerId", out _)); // a new owner needs no OwnerId
        Assert.Equal(before, factory.WithDb(db => db.Pets.Count()));
    }

    [Fact]
    public async Task Quick_create_without_antiforgery_token_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/Pets/QuickCreate",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Intruso", ["Species"] = "Perro" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(factory.WithDb(db => db.Pets.Any(p => p.Name == "Intruso")));
    }

    [Fact]
    public async Task Pet_form_rejects_a_photo_that_is_not_an_image()
    {
        var ownerId = factory.WithDb(db => TestData.AddPet(db, "Otra mascota").OwnerId);
        var client = factory.CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/Pets/Create");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Kiwi"), "Name" },
            { new StringContent("Ave"), "Species" },
            { new StringContent(ownerId.ToString()), "OwnerId" },
            { new ByteArrayContent("%PDF-1.7 fake"u8.ToArray()), "Photo", "kiwi.png" }
        };
        var response = await client.PostAsync("/Pets/Create", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // form re-rendered
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("no es una imagen válida", html);
        Assert.False(factory.WithDb(db => db.Pets.Any(p => p.Name == "Kiwi")));
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token not found in the form.");
        return match.Groups[1].Value;
    }
}
