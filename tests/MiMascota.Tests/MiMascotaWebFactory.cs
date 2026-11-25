using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Security;

namespace MiMascota.Tests;

/// <summary>
/// Runs the real app (Development, so the two demo staff accounts are seeded) against a temporary
/// SQLite file without demo clinic data. Staff clients log in through the real login form.
/// </summary>
public class MiMascotaWebFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"mimascota-tests-{Guid.NewGuid():N}.db");

    /// <summary>Uploaded photos go here instead of App_Data.</summary>
    public string PhotosRoot { get; } = Path.Combine(Path.GetTempPath(), $"mimascota-tests-photos-{Guid.NewGuid():N}");

    protected virtual string Environment => "Development";

    /// <summary>Settings a derived factory adds or overrides.</summary>
    protected virtual Dictionary<string, string?> ExtraSettings => [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:SeedDemoData"] = "false",
                ["ConnectionStrings:Sqlite"] = $"Data Source={_databasePath}",
                ["PetPhotos:Folder"] = PhotosRoot,
                // Many tests post the login and contact forms: the limits get their own tests.
                ["RateLimits:LoginPerMinute"] = "100000",
                ["RateLimits:ContactPerMinute"] = "100000"
            });
            config.AddInMemoryCollection(ExtraSettings);
        });
    }

    /// <summary>A client logged in as one of the demo accounts (Recepción by default).</summary>
    public async Task<HttpClient> CreateStaffClientAsync(string role = Roles.Reception, bool allowAutoRedirect = true)
    {
        var (email, password) = role == Roles.Admin
            ? (StaffSeeder.DemoAdminEmail, StaffSeeder.DemoAdminPassword)
            : (StaffSeeder.DemoReceptionEmail, StaffSeeder.DemoReceptionPassword);
        return await CreateClientLoggedInAsync(email, password, allowAutoRedirect);
    }

    public async Task<HttpClient> CreateClientLoggedInAsync(string email, string password, bool allowAutoRedirect = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });
        var response = await LoginAsync(client, email, password);

        var loggedIn = allowAutoRedirect
            ? response.StatusCode == HttpStatusCode.OK && response.RequestMessage?.RequestUri?.AbsolutePath != "/cuenta/entrar"
            : response.StatusCode == HttpStatusCode.Redirect;
        Assert.True(loggedIn, $"Login as {email} failed: {response.StatusCode} {response.RequestMessage?.RequestUri}");
        return client;
    }

    /// <summary>Posts the login form (with its antiforgery token) and returns the response.</summary>
    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password,
        bool rememberMe = false, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/cuenta/entrar" : $"/cuenta/entrar?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var token = await GetAntiforgeryTokenAsync(client, url);

        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Email"] = email,
            ["Password"] = password,
            ["RememberMe"] = rememberMe ? "true" : "false"
        };
        if (returnUrl is not null) fields["ReturnUrl"] = returnUrl;

        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    /// <summary>Creates a staff account directly (for tests that lock or disable it).</summary>
    public async Task<string> CreateUserAsync(string email, string password, string role, string fullName = "Persona de prueba")
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName };
        var result = await users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);
        return user.Id;
    }

    /// <summary>Runs code against the app's database (for test setup).</summary>
    public T WithDb<T>(Func<AppDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithUsersAsync<T>(Func<UserManager<ApplicationUser>, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Antiforgery token not found in {url}.");
        return match.Groups[1].Value;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { File.Delete(_databasePath); } catch (IOException) { /* best effort */ }
        try { if (Directory.Exists(PhotosRoot)) Directory.Delete(PhotosRoot, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
