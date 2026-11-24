using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:SeedDemoData"] = "false",
            ["ConnectionStrings:Sqlite"] = $"Data Source={_databasePath}"
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

        foreach (var url in new[] { "/", "/Appointments", "/Appointments/Create", "/Owners", "/Pets", "/Services" })
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

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token not found in the form.");
        return match.Groups[1].Value;
    }
}
