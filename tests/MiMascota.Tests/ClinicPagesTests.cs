using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Security;

namespace MiMascota.Tests;

/// <summary>Footer links, the clinic info pages and the contact form, against the real app.</summary>
public class ClinicPagesTests(MiMascotaWebFactory factory) : IClassFixture<MiMascotaWebFactory>
{
    private static string Footer(string html)
    {
        var match = Regex.Match(html, "<footer class=\"mm-footer\">(.*?)</footer>", RegexOptions.Singleline);
        Assert.True(match.Success, "Footer not found.");
        return match.Groups[1].Value;
    }

    private static List<string> Hrefs(string html) =>
        Regex.Matches(html, "href=\"([^\"]*)\"").Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).ToList();

    [Theory]
    [InlineData("/", false)]
    [InlineData("/clinica/nosotros", false)]
    [InlineData("/contacto", false)]
    [InlineData("/", true)]
    [InlineData("/Pets", true)]
    public async Task Every_footer_link_returns_200(string page, bool asStaff)
    {
        // No automatic redirects: a staff page answering 302 → login would not count as 200.
        var client = asStaff
            ? await factory.CreateStaffClientAsync(allowAutoRedirect: false)
            : factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var links = Hrefs(Footer(await client.GetStringAsync(page)));

        Assert.DoesNotContain("#", links);       // no dead "#" links
        Assert.True(links.Count >= 20, $"Only {links.Count} footer links found.");

        foreach (var link in links.Where(l => l.StartsWith('/') || l.StartsWith('#')).Distinct())
        {
            var path = link.Split('#')[0];
            if (path.Length == 0) path = page; // "#equipo" on the same page
            var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{link} (from {page}) returned {response.StatusCode}");
        }
    }

    [Theory]
    [InlineData("/clinica/nosotros", "Nuestra historia")]
    [InlineData("/clinica/horarios", "Semana de atención")]
    [InlineData("/clinica/como-llegar", "openstreetmap.org/export/embed.html")]
    [InlineData("/ayuda/preguntas", "Preguntas frecuentes")]
    [InlineData("/ayuda/terminos", "Términos de uso")]
    [InlineData("/ayuda/privacidad", "derechos ARCO")]
    [InlineData("/contacto", "Envíanos un mensaje")]
    public async Task Info_pages_render(string url, string expectedText)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedText, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task About_page_has_the_team_anchor_and_the_footer_points_to_it()
    {
        var html = await factory.CreateClient().GetStringAsync("/clinica/nosotros");

        Assert.Contains("id=\"equipo\"", html);
        Assert.Contains("href=\"#equipo\"", Footer(html));
    }

    [Fact]
    public async Task Footer_links_to_home_team_section_from_other_pages()
    {
        var html = await factory.CreateClient().GetStringAsync("/clinica/horarios");
        Assert.Contains("href=\"/#equipo\"", Footer(html));

        var staffHtml = await (await factory.CreateStaffClientAsync()).GetStringAsync("/Pets");
        Assert.Contains("href=\"/#equipo\"", Footer(staffHtml));

        var home = await factory.CreateClient().GetStringAsync("/");
        Assert.Contains("id=\"equipo\"", home);
    }

    [Fact]
    public async Task Hours_page_shows_the_weekly_table_and_an_open_or_closed_badge()
    {
        var html = WebUtility.HtmlDecode(await factory.CreateClient().GetStringAsync("/clinica/horarios"));

        Assert.Matches("Abierto ahora|Cerrado", html);
        Assert.Contains("open-badge", html);
        foreach (var day in new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" })
        {
            Assert.Contains(day, html);
        }
        Assert.Contains("9:00 – 18:00", html);
        Assert.Contains("18 horarios", html);
    }

    [Fact]
    public async Task Directions_page_links_to_google_maps()
    {
        var html = await factory.CreateClient().GetStringAsync("/clinica/como-llegar");

        Assert.Contains("https://www.google.com/maps/search/?api=1&amp;query=Tantoyuca%2C%20Veracruz", html);
        Assert.Contains("Centro, Tantoyuca, Veracruz", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task Footer_service_links_preselect_the_service_or_fall_back_to_the_list()
    {
        var (vaccineId, exoticId) = factory.WithDb(db =>
        {
            // Only these two exist here; "Consulta general" and "Baño y estética" are missing.
            db.VetServices.RemoveRange(db.VetServices.Where(s =>
                s.Name == "Consulta general" || s.Name == "Baño y estética"));
            db.SaveChanges();
            var vaccine = db.VetServices.FirstOrDefault(s => s.Name == "Vacunación") ?? TestData.AddService(db, 30, "Vacunación");
            var exotic = db.VetServices.FirstOrDefault(s => s.Name == "Consulta exóticos") ?? TestData.AddService(db, 30, "Consulta exóticos");
            return (vaccine.Id, exotic.Id);
        });

        // Staff: the booking form with the service selected.
        var staffFooter = Footer(await (await factory.CreateStaffClientAsync()).GetStringAsync("/"));
        Assert.Contains($"href=\"/Appointments/Create?serviceId={vaccineId}\">Vacunas</a>", staffFooter);
        Assert.Contains($"href=\"/Appointments/Create?serviceId={exoticId}\">Exóticos</a>", WebUtility.HtmlDecode(staffFooter));
        Assert.Contains("href=\"/Services\">Consulta general</a>", staffFooter);

        // Visitors: the contact page with the service written in the message.
        var publicFooter = Footer(await factory.CreateClient().GetStringAsync("/"));
        Assert.Contains("href=\"/contacto?servicio=Vacunaci%C3%B3n\">Vacunas</a>", publicFooter);
        Assert.Contains("href=\"/contacto?servicio=Consulta%20ex%C3%B3ticos\">Exóticos</a>", WebUtility.HtmlDecode(publicFooter));
        Assert.Contains("href=\"/Services\">Consulta general</a>", publicFooter);
        Assert.DoesNotContain("/Appointments", publicFooter);
    }

    [Fact]
    public async Task Social_icons_without_an_account_are_not_links()
    {
        var footer = WebUtility.HtmlDecode(Footer(await factory.CreateClient().GetStringAsync("/")));
        var social = Regex.Match(footer, "<div class=\"footer-social\">(.*?)</div>", RegexOptions.Singleline).Groups[1].Value;

        Assert.Equal(3, Regex.Matches(social, "<span class=\"social-soon\"[^>]*title=\"Próximamente\"").Count);
        Assert.DoesNotContain("href=\"#\"", social);
        Assert.Matches("<a [^>]*href=\"/contacto\"[^>]*><i class=\"bi bi-whatsapp\"", social);
    }

    [Fact]
    public async Task Contact_form_saves_the_message_and_redirects_with_a_success_message()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetAntiforgeryTokenAsync(client, "/contacto");
        var name = $"Prueba {Guid.NewGuid():N}"[..20];

        var response = await client.PostAsync("/contacto", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = name,
            ["Contact"] = "789 123 4567",
            ["Message"] = "Quiero cambiar la cita de Firulais al viernes."
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/contacto", response.Headers.Location?.OriginalString);

        var saved = factory.WithDb(db => db.ContactMessages.Single(m => m.Name == name));
        Assert.Equal("789 123 4567", saved.Contact);
        Assert.False(saved.IsRead);

        // Post/Redirect/Get: the success message shows once on the redirected page.
        var follow = await client.GetAsync("/contacto");
        Assert.Contains($"¡Gracias, {name}!", WebUtility.HtmlDecode(await follow.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("", "789 123 4567", "Un mensaje suficientemente largo.", "Escribe tu nombre.")]
    [InlineData("Ana", "", "Un mensaje suficientemente largo.", "Déjanos un teléfono o un correo")]
    [InlineData("Ana", "no-es-contacto", "Un mensaje suficientemente largo.", "Escribe un teléfono")]
    [InlineData("Ana", "123", "Un mensaje suficientemente largo.", "Escribe un teléfono")]
    [InlineData("Ana", "ana@example.com", "Hola", "El mensaje debe tener entre 10 y 1000 caracteres.")]
    public async Task Contact_form_rejects_invalid_input(string name, string contact, string message, string expectedError)
    {
        var client = factory.CreateClient();
        var token = await GetAntiforgeryTokenAsync(client, "/contacto");
        var before = factory.WithDb(db => db.ContactMessages.Count());

        var response = await client.PostAsync("/contacto", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = name,
            ["Contact"] = contact,
            ["Message"] = message
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // form re-rendered
        Assert.Contains(expectedError, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(before, factory.WithDb(db => db.ContactMessages.Count()));
    }

    [Theory]
    [InlineData("ana@example.com")]
    [InlineData("+52 789 123 4567")]
    [InlineData("(789) 123-4567")]
    public async Task Contact_accepts_a_phone_or_an_email(string contact)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetAntiforgeryTokenAsync(client, "/contacto");

        var response = await client.PostAsync("/contacto", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Ana",
            ["Contact"] = contact,
            ["Message"] = "¿Atienden iguanas los sábados?"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task Contact_form_without_antiforgery_token_is_rejected()
    {
        var response = await factory.CreateClient().PostAsync("/contacto", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = "Intruso",
            ["Contact"] = "intruso@example.com",
            ["Message"] = "Mensaje sin token de seguridad."
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(factory.WithDb(db => db.ContactMessages.Any(m => m.Name == "Intruso")));
    }

    [Fact]
    public async Task Inbox_lists_newest_first_and_marks_messages_as_read()
    {
        var (olderId, newerId) = factory.WithDb(db =>
        {
            var older = new ContactMessage { Name = "Mensaje viejo", Contact = "a@example.com", Message = "Primero en llegar", CreatedAt = new DateTime(2030, 1, 1, 9, 0, 0) };
            var newer = new ContactMessage { Name = "Mensaje nuevo", Contact = "b@example.com", Message = "Segundo en llegar", CreatedAt = new DateTime(2030, 1, 2, 9, 0, 0) };
            db.ContactMessages.AddRange(older, newer);
            db.SaveChanges();
            return (older.Id, newer.Id);
        });
        var client = await factory.CreateStaffClientAsync(allowAutoRedirect: false);

        var html = await client.GetStringAsync("/contacto/mensajes");
        Assert.True(html.IndexOf("Mensaje nuevo", StringComparison.Ordinal) < html.IndexOf("Mensaje viejo", StringComparison.Ordinal));

        var token = await GetAntiforgeryTokenAsync(client, "/contacto/mensajes");
        var response = await client.PostAsync($"/contacto/mensajes/{olderId}/leido",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(factory.WithDb(db => db.ContactMessages.Find(olderId)!.IsRead));
        Assert.False(factory.WithDb(db => db.ContactMessages.Find(newerId)!.IsRead));
    }

    [Fact]
    public async Task An_old_sqlite_database_gets_the_new_tables_at_startup_and_staff_can_log_in()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mimascota-old-{Guid.NewGuid():N}.db");
        var oldPhoto = $"{Guid.NewGuid():N}.webp";
        try
        {
            // Build a current database, then drop the newer tables to simulate a mimascota.db
            // from before the contact form and the staff accounts, with a photo in the old public folder.
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path}").Options;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                foreach (var table in new[] { "ContactMessages", "AspNetUserTokens", "AspNetUserRoles", "AspNetUserLogins",
                                              "AspNetUserClaims", "AspNetRoleClaims", "AspNetUsers", "AspNetRoles" })
                {
                    await db.Database.ExecuteSqlRawAsync("DROP TABLE \"" + table + "\"");
                }
                var pet = TestData.AddPet(db, "Foto vieja");
                pet.PhotoPath = "/uploads/mascotas/" + oldPhoto;
                await db.SaveChangesAsync();
            }

            await using var app = new OldDatabaseFactory(path);
            var client = await app.CreateStaffClientAsync(Roles.Admin);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/contacto/mensajes")).StatusCode);
            Assert.Equal("/fotos/mascotas/" + oldPhoto, app.WithDb(db => db.Pets.Single(p => p.Name == "Foto vieja").PhotoPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(path); } catch (IOException) { /* best effort */ }
        }
    }

    /// <summary>The normal test app, but on an existing database file.</summary>
    private sealed class OldDatabaseFactory(string path) : MiMascotaWebFactory
    {
        protected override Dictionary<string, string?> ExtraSettings => new()
        {
            ["ConnectionStrings:Sqlite"] = $"Data Source={path}"
        };
    }

    private static Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url) =>
        MiMascotaWebFactory.GetAntiforgeryTokenAsync(client, url);
}
