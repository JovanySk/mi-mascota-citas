using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using MiMascota.Web.Controllers;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Security;

namespace MiMascota.Tests;

/// <summary>Login, roles, what is public and what is staff-only, photos, rate limits and headers.</summary>
public class SecurityTests(MiMascotaWebFactory factory) : IClassFixture<MiMascotaWebFactory>
{
    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string LocationPath(HttpResponseMessage response) =>
        response.Headers.Location is { } location
            ? (location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString)
            : string.Empty;

    private static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20] + "@mimascota.test";

    private static async Task<string> Html(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    // ─────────────── Anonymous visitors ───────────────

    [Theory]
    [InlineData("/Appointments")]
    [InlineData("/Appointments/Create")]
    [InlineData("/Appointments/Availability?date=2030-01-07&serviceId=1")]
    [InlineData("/Owners")]
    [InlineData("/Owners/Create")]
    [InlineData("/Owners/Edit/1")]
    [InlineData("/Pets")]
    [InlineData("/Pets/Create")]
    [InlineData("/Services/Create")]
    [InlineData("/Services/Edit/1")]
    [InlineData("/contacto/mensajes")]
    [InlineData("/personal")]
    [InlineData("/cuenta/contrasena")]
    [InlineData("/fotos/mascotas/0123456789abcdef0123456789abcdef.webp")]
    public async Task Anonymous_visitors_are_sent_to_the_login_page(string url)
    {
        var response = await Anonymous().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/cuenta/entrar?ReturnUrl=", LocationPath(response));
    }

    [Theory]
    [InlineData("/Pets/QuickCreate")]
    [InlineData("/Appointments/ChangeStatus/1")]
    [InlineData("/Appointments/Create")]
    [InlineData("/Owners/Delete/1")]
    [InlineData("/Pets/Delete/1")]
    [InlineData("/Services/Delete/1")]
    [InlineData("/contacto/mensajes/1/leido")]
    [InlineData("/personal")]
    [InlineData("/cuenta/salir")]
    public async Task Anonymous_posts_to_staff_endpoints_are_sent_to_the_login_page(string url)
    {
        var response = await Anonymous().PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["status"] = "Confirmed",
            ["Name"] = "Intruso"
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/cuenta/entrar", LocationPath(response));
        Assert.False(factory.WithDb(db => db.Pets.Any(p => p.Name == "Intruso")));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Home/Index")]
    [InlineData("/Home/Error")]
    [InlineData("/Services")]
    [InlineData("/clinica/nosotros")]
    [InlineData("/clinica/horarios")]
    [InlineData("/clinica/como-llegar")]
    [InlineData("/ayuda/preguntas")]
    [InlineData("/ayuda/terminos")]
    [InlineData("/ayuda/privacidad")]
    [InlineData("/contacto")]
    [InlineData("/contacto?servicio=Vacunación")]
    [InlineData("/cuenta/entrar")]
    [InlineData("/css/site.css")]
    [InlineData("/img/paw.svg")]
    public async Task Public_pages_are_open_to_everyone(string url)
    {
        var response = await Anonymous().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Access_denied_page_is_public_and_answers_403()
    {
        var response = await Anonymous().GetAsync("/cuenta/acceso-denegado");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Acceso denegado", await Html(response));
    }

    [Fact]
    public async Task Anonymous_nav_shows_public_links_and_the_login_button()
    {
        var html = await Html(await Anonymous().GetAsync("/clinica/horarios"));
        var nav = html[..html.IndexOf("</header>", StringComparison.Ordinal)];

        Assert.Contains("href=\"/cuenta/entrar\"", nav);
        Assert.Contains(">Horarios</a>", nav);
        Assert.Contains(">Contacto</a>", nav);
        Assert.DoesNotContain("/Appointments", nav);
        Assert.DoesNotContain("/Owners", nav);
        Assert.DoesNotContain("Mensajes", nav);
        Assert.DoesNotContain("Personal</a>", nav);
    }

    // ─────────────── Home: public vs staff ───────────────

    [Fact]
    public async Task Home_hides_todays_agenda_from_visitors_and_shows_it_to_staff()
    {
        var ownerName = "Dueña Confidencial " + Guid.NewGuid().ToString("N")[..6];
        factory.WithDb(db =>
        {
            var service = TestData.AddService(db, 30, "Consulta " + Guid.NewGuid().ToString("N")[..6]);
            var pet = new Pet { Name = "Mascota de hoy", Species = "Perro", Owner = new Owner { Name = ownerName, Phone = "7891234567" } };
            db.Pets.Add(pet);
            db.SaveChanges();
            return TestData.AddAppointment(db, pet, service, DateOnly.FromDateTime(DateTime.Now).At(12));
        });

        var publicHtml = await Html(await Anonymous().GetAsync("/"));
        Assert.DoesNotContain("Agenda de hoy", publicHtml);
        Assert.DoesNotContain("Nueva cita rápida", publicHtml);
        Assert.DoesNotContain(ownerName.Split(' ')[0] + " " + ownerName.Split(' ')[1], publicHtml);
        Assert.DoesNotContain("Mascota de hoy", publicHtml);
        Assert.DoesNotContain("citas hoy", publicHtml);
        Assert.DoesNotContain("ChangeStatus", publicHtml);
        Assert.Contains("¿Quieres agendar?", publicHtml);
        Assert.Contains("Solicita tu cita", publicHtml);
        Assert.Contains("href=\"/contacto?motivo=cita\"", publicHtml);
        Assert.Contains("especies atendidas", publicHtml);

        var staffHtml = await Html(await (await factory.CreateStaffClientAsync()).GetAsync("/"));
        Assert.Contains("Agenda de hoy", staffHtml);
        Assert.Contains("Nueva cita rápida", staffHtml);
        Assert.Contains("Mascota de hoy", staffHtml);
        Assert.Contains("dueño: Dueña Confidencial", staffHtml);
        Assert.DoesNotContain("¿Quieres agendar?", staffHtml);
    }

    [Fact]
    public async Task Public_booking_buttons_go_to_the_contact_page_with_the_service()
    {
        factory.WithDb(db => db.VetServices.FirstOrDefault(s => s.Name == "Baño y estética") ?? TestData.AddService(db, 60, "Baño y estética"));

        var publicHtml = await Anonymous().GetStringAsync("/");
        Assert.Contains("href=\"/contacto?servicio=Ba%C3%B1o%20y%20est%C3%A9tica\"", publicHtml); // "Reservar baño"
        Assert.DoesNotContain("/Appointments/Create", publicHtml);

        var staffHtml = await (await factory.CreateStaffClientAsync()).GetStringAsync("/");
        Assert.Contains("href=\"/Appointments/Create?serviceId=", staffHtml);
    }

    [Fact]
    public async Task Contact_page_prefills_the_requested_service()
    {
        factory.WithDb(db => db.VetServices.FirstOrDefault(s => s.Name == "Baño y estética") ?? TestData.AddService(db, 60, "Baño y estética"));

        var html = await Html(await Anonymous().GetAsync("/contacto?servicio=Ba%C3%B1o%20y%20est%C3%A9tica"));
        Assert.Contains("Hola, quiero agendar una cita de «Baño y estética» para mi mascota.", html);
        Assert.Contains("Servicio: Baño y estética", html);

        var generic = await Html(await Anonymous().GetAsync("/contacto?motivo=cita"));
        Assert.Contains("Hola, quiero agendar una cita para mi mascota.", generic);

        // Text that is not a real service is never copied into the form.
        var unknown = await Html(await Anonymous().GetAsync("/contacto?servicio=Llama%20al%20555-000"));
        Assert.DoesNotContain("Llama al 555-000", unknown);
        Assert.Contains("Hola, quiero agendar una cita para mi mascota.", unknown);
    }

    // ─────────────── Roles ───────────────

    [Theory]
    [InlineData("/personal")]
    [InlineData("/Services/Create")]
    [InlineData("/Services/Edit/1")]
    public async Task Reception_cannot_open_admin_pages(string url)
    {
        var client = await factory.CreateStaffClientAsync(Roles.Reception, allowAutoRedirect: false);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/cuenta/acceso-denegado", LocationPath(response));
    }

    [Fact]
    public async Task Reception_cannot_create_or_delete_services()
    {
        var serviceId = factory.WithDb(db => TestData.AddService(db, 30, "Servicio protegido").Id);
        var client = await factory.CreateStaffClientAsync(Roles.Reception, allowAutoRedirect: false);
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(client, "/Appointments/Create");

        var delete = await client.PostAsync($"/Services/Delete/{serviceId}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        var create = await client.PostAsync("/Services/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Name"] = "Servicio colado",
            ["DurationMinutes"] = "30",
            ["Price"] = "100"
        }));

        Assert.StartsWith("/cuenta/acceso-denegado", LocationPath(delete));
        Assert.StartsWith("/cuenta/acceso-denegado", LocationPath(create));
        Assert.True(factory.WithDb(db => db.VetServices.Any(s => s.Id == serviceId)));
        Assert.False(factory.WithDb(db => db.VetServices.Any(s => s.Name == "Servicio colado")));
    }

    [Theory]
    [InlineData("/Appointments")]
    [InlineData("/Appointments/Create")]
    [InlineData("/Owners")]
    [InlineData("/Pets")]
    [InlineData("/Services")]
    [InlineData("/contacto/mensajes")]
    [InlineData("/cuenta/contrasena")]
    public async Task Reception_can_use_the_daily_pages(string url)
    {
        var client = await factory.CreateStaffClientAsync(Roles.Reception, allowAutoRedirect: false);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Services_list_shows_edit_buttons_only_to_the_administrator()
    {
        var id = factory.WithDb(db => TestData.AddService(db, 30, "Servicio visible").Id);

        var anonymous = await Anonymous().GetStringAsync("/Services");
        var reception = await (await factory.CreateStaffClientAsync(Roles.Reception)).GetStringAsync("/Services");
        var admin = await (await factory.CreateStaffClientAsync(Roles.Admin)).GetStringAsync("/Services");

        Assert.DoesNotContain($"/Services/Edit/{id}", anonymous);
        Assert.DoesNotContain($"/Services/Edit/{id}", reception);
        Assert.Contains($"/Services/Edit/{id}", admin);
        Assert.Contains("/Services/Create", admin);
        Assert.Contains("Solicitar cita", WebUtility.HtmlDecode(anonymous));
    }

    [Theory]
    [InlineData("/personal")]
    [InlineData("/Services/Create")]
    [InlineData("/Appointments")]
    [InlineData("/contacto/mensajes")]
    public async Task Administrator_can_open_everything(string url)
    {
        var client = await factory.CreateStaffClientAsync(Roles.Admin, allowAutoRedirect: false);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Staff_nav_shows_the_system_and_personal_only_for_the_administrator()
    {
        var reception = await (await factory.CreateStaffClientAsync(Roles.Reception)).GetStringAsync("/clinica/horarios");
        var admin = await (await factory.CreateStaffClientAsync(Roles.Admin)).GetStringAsync("/clinica/horarios");
        static string Nav(string html) => WebUtility.HtmlDecode(html[..html.IndexOf("</header>", StringComparison.Ordinal)]);

        Assert.Contains("href=\"/Appointments\"", Nav(reception));
        Assert.Contains("href=\"/contacto/mensajes\"", Nav(reception));
        Assert.Contains("Recepción", Nav(reception));
        Assert.Contains("action=\"/cuenta/salir\"", Nav(reception));
        Assert.DoesNotContain("href=\"/personal\"", Nav(reception));
        Assert.DoesNotContain("href=\"/cuenta/entrar\"", Nav(reception));

        Assert.Contains("href=\"/personal\"", Nav(admin));
        Assert.Contains("Administrador", Nav(admin));
    }

    // ─────────────── /personal ───────────────

    [Fact]
    public async Task Administrator_creates_a_staff_account_that_must_change_its_password()
    {
        var admin = await factory.CreateStaffClientAsync(Roles.Admin, allowAutoRedirect: false);
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(admin, "/personal");
        var email = UniqueEmail("nueva");

        var response = await admin.PostAsync("/personal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["NewStaff.FullName"] = "Laura Nueva",
            ["NewStaff.Email"] = email,
            ["NewStaff.Role"] = Roles.Reception,
            ["NewStaff.TemporaryPassword"] = "Temporal123"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Laura Nueva", await admin.GetStringAsync("/personal"));

        // First login goes to "change your password".
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await MiMascotaWebFactory.LoginAsync(client, email, "Temporal123");
        Assert.Equal("/cuenta/contrasena", LocationPath(login));

        var changeToken = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(client, "/cuenta/contrasena");
        var change = await client.PostAsync("/cuenta/contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = changeToken,
            ["CurrentPassword"] = "Temporal123",
            ["NewPassword"] = "MiClaveNueva9",
            ["ConfirmPassword"] = "MiClaveNueva9"
        }));
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode);
        Assert.False(await factory.WithUsersAsync(async users => (await users.FindByEmailAsync(email))!.MustChangePassword));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Appointments")).StatusCode);
    }

    [Theory]
    [InlineData("corta1", "al menos 8 caracteres")]
    [InlineData("soloLetras", "al menos un número")]
    [InlineData("12345678", "al menos una letra")]
    public async Task Weak_passwords_are_rejected_with_spanish_messages(string password, string expected)
    {
        var admin = await factory.CreateStaffClientAsync(Roles.Admin, allowAutoRedirect: false);
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(admin, "/personal");
        var email = UniqueEmail("debil");

        var response = await admin.PostAsync("/personal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["NewStaff.FullName"] = "Clave Débil",
            ["NewStaff.Email"] = email,
            ["NewStaff.Role"] = Roles.Reception,
            ["NewStaff.TemporaryPassword"] = password
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // form re-rendered
        Assert.Contains(expected, await Html(response));
        Assert.Null(await factory.WithUsersAsync(users => users.FindByEmailAsync(email)));
    }

    [Fact]
    public async Task Duplicate_emails_are_rejected()
    {
        var admin = await factory.CreateStaffClientAsync(Roles.Admin, allowAutoRedirect: false);
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(admin, "/personal");

        var response = await admin.PostAsync("/personal", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["NewStaff.FullName"] = "Copia",
            ["NewStaff.Email"] = StaffSeeder.DemoReceptionEmail,
            ["NewStaff.Role"] = Roles.Reception,
            ["NewStaff.TemporaryPassword"] = "Temporal123"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ya existe una cuenta con el correo", await Html(response));
    }

    [Fact]
    public async Task Administrator_disables_and_enables_an_account()
    {
        var email = UniqueEmail("baja");
        var userId = await factory.CreateUserAsync(email, "Recepcion456", Roles.Reception);
        var victim = await factory.CreateClientLoggedInAsync(email, "Recepcion456", allowAutoRedirect: false);
        var admin = await factory.CreateStaffClientAsync(Roles.Admin, allowAutoRedirect: false);
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(admin, "/personal");

        var disable = await admin.PostAsync($"/personal/{userId}/desactivar",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, disable.StatusCode);
        Assert.Contains("Desactivada", await admin.GetStringAsync("/personal"));

        // Can no longer log in (the open session ends at the next security-stamp check).
        var retry = await MiMascotaWebFactory.LoginAsync(Anonymous(), email, "Recepcion456");
        Assert.Contains(AccountController.LockedOutMessage, await Html(retry));

        var enable = await admin.PostAsync($"/personal/{userId}/activar",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, enable.StatusCode);
        var again = await MiMascotaWebFactory.LoginAsync(Anonymous(), email, "Recepcion456");
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        _ = victim;
    }

    [Fact]
    public async Task Administrator_cannot_disable_their_own_account()
    {
        var email = UniqueEmail("jefa");
        var adminId = await factory.CreateUserAsync(email, "Admin4567", Roles.Admin);
        var admin = await factory.CreateClientLoggedInAsync(email, "Admin4567", allowAutoRedirect: false);
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(admin, "/personal");

        var response = await admin.PostAsync($"/personal/{adminId}/desactivar",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("No puedes desactivar tu propia cuenta", await Html(await admin.GetAsync("/personal")));
        Assert.Null(await factory.WithUsersAsync(async users => (await users.FindByIdAsync(adminId))!.LockoutEnd));
    }

    // ─────────────── Login, lockout, logout ───────────────

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_generic_message()
    {
        var wrongPassword = await MiMascotaWebFactory.LoginAsync(Anonymous(), StaffSeeder.DemoReceptionEmail, "NoEsLaClave1");
        var unknownEmail = await MiMascotaWebFactory.LoginAsync(Anonymous(), "nadie@mimascota.test", "NoEsLaClave1");

        Assert.Equal(HttpStatusCode.OK, wrongPassword.StatusCode);
        Assert.Contains(AccountController.InvalidLogin, await Html(wrongPassword));
        Assert.Contains(AccountController.InvalidLogin, await Html(unknownEmail));

        // Reset the counter so the shared demo account never gets locked by this test.
        await factory.WithUsersAsync(async users =>
        {
            var user = await users.FindByEmailAsync(StaffSeeder.DemoReceptionEmail);
            return await users.ResetAccessFailedCountAsync(user!);
        });
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_15_minutes()
    {
        var email = UniqueEmail("bloqueo");
        await factory.CreateUserAsync(email, "Correcta123", Roles.Reception);

        for (var i = 1; i < StaffSecuritySetup.MaxFailedAttempts; i++)
        {
            var attempt = await MiMascotaWebFactory.LoginAsync(Anonymous(), email, "Equivocada1");
            Assert.Contains(AccountController.InvalidLogin, await Html(attempt));
        }

        var fifth = await MiMascotaWebFactory.LoginAsync(Anonymous(), email, "Equivocada1");
        Assert.Contains(AccountController.LockedOutMessage, await Html(fifth));

        // Even the right password is refused while the lockout lasts.
        var right = await MiMascotaWebFactory.LoginAsync(Anonymous(), email, "Correcta123");
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Contains(AccountController.LockedOutMessage, await Html(right));

        var lockoutEnd = await factory.WithUsersAsync(async users => (await users.FindByEmailAsync(email))!.LockoutEnd);
        Assert.NotNull(lockoutEnd);
        Assert.InRange(lockoutEnd!.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.1));
    }

    [Fact]
    public async Task Login_cookie_is_http_only_and_lax()
    {
        var response = await MiMascotaWebFactory.LoginAsync(Anonymous(), StaffSeeder.DemoReceptionEmail, StaffSeeder.DemoReceptionPassword);

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("MiMascota.Personal=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires", cookie, StringComparison.OrdinalIgnoreCase); // no "recordarme": session cookie
    }

    [Fact]
    public async Task Remember_me_keeps_the_cookie_for_8_hours()
    {
        var response = await MiMascotaWebFactory.LoginAsync(Anonymous(), StaffSeeder.DemoReceptionEmail,
            StaffSeeder.DemoReceptionPassword, rememberMe: true);

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("MiMascota.Personal=", StringComparison.Ordinal));
        var expires = DateTimeOffset.Parse(System.Text.RegularExpressions.Regex.Match(cookie, "expires=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value);
        Assert.InRange(expires - DateTimeOffset.UtcNow, TimeSpan.FromHours(7.9), TimeSpan.FromHours(8.1));
    }

    [Fact]
    public async Task Login_only_redirects_to_local_urls()
    {
        var local = await MiMascotaWebFactory.LoginAsync(Anonymous(), StaffSeeder.DemoReceptionEmail,
            StaffSeeder.DemoReceptionPassword, returnUrl: "/Pets");
        var external = await MiMascotaWebFactory.LoginAsync(Anonymous(), StaffSeeder.DemoReceptionEmail,
            StaffSeeder.DemoReceptionPassword, returnUrl: "https://evil.example.com/");

        Assert.Equal("/Pets", LocationPath(local));
        Assert.Equal("/", LocationPath(external));
    }

    [Fact]
    public async Task Logout_needs_a_post_with_the_antiforgery_token()
    {
        var client = await factory.CreateStaffClientAsync(allowAutoRedirect: false);

        // GET does nothing (no such endpoint).
        var get = await client.GetAsync("/cuenta/salir");
        Assert.False(get.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Appointments")).StatusCode);

        // POST without the token is rejected and the session stays.
        var noToken = await client.PostAsync("/cuenta/salir", new FormUrlEncodedContent([]));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Appointments")).StatusCode);

        // POST with the token ends the session.
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(client, "/Appointments/Create");
        var logout = await client.PostAsync("/cuenta/salir",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", LocationPath(logout));

        var after = await client.GetAsync("/Appointments");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.StartsWith("/cuenta/entrar", LocationPath(after));
    }

    [Fact]
    public async Task Changing_the_password_requires_the_current_one()
    {
        var email = UniqueEmail("cambio");
        await factory.CreateUserAsync(email, "Original123", Roles.Reception);
        var client = await factory.CreateClientLoggedInAsync(email, "Original123", allowAutoRedirect: false);
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(client, "/cuenta/contrasena");

        var response = await client.PostAsync("/cuenta/contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["CurrentPassword"] = "NoEsLaActual1",
            ["NewPassword"] = "OtraClave123",
            ["ConfirmPassword"] = "OtraClave123"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("La contraseña actual no es correcta.", await Html(response));
    }

    // ─────────────── Pet photos ───────────────

    [Fact]
    public async Task Pet_photos_are_served_only_to_staff_and_never_as_static_files()
    {
        var ownerId = factory.WithDb(db => TestData.AddPet(db, "Con foto").OwnerId);
        var staff = await factory.CreateStaffClientAsync(allowAutoRedirect: false);
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(staff, "/Appointments/Create");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent("Fotogénica"), "Name" },
            { new StringContent("Gato"), "Species" },
            { new StringContent("false"), "NewOwner" },
            { new StringContent(ownerId.ToString()), "OwnerId" },
            { new ByteArrayContent(PetPhotoStorageTests.RealImage()), "Photo", "gata.png" }
        };
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync("/Pets/QuickCreate", form)).StatusCode);

        var photoPath = factory.WithDb(db => db.Pets.Single(p => p.Name == "Fotogénica").PhotoPath)!;
        Assert.StartsWith("/fotos/mascotas/", photoPath);
        var fileName = Path.GetFileName(photoPath);

        // Staff: 200, WebP, private and revalidated on every view.
        var forStaff = await staff.GetAsync(photoPath);
        Assert.Equal(HttpStatusCode.OK, forStaff.StatusCode);
        Assert.Equal("image/webp", forStaff.Content.Headers.ContentType?.MediaType);
        Assert.True(forStaff.Headers.CacheControl?.Private);
        Assert.True(forStaff.Headers.CacheControl?.NoCache);
        Assert.NotNull(forStaff.Headers.ETag); // revalidation is a cheap 304
        Assert.Equal(".webp", MiMascota.Web.Services.PetPhotoStorage.DetectExtension(await forStaff.Content.ReadAsByteArrayAsync()));

        // Visitors: login page. The old public URL does not exist any more.
        var forVisitor = await Anonymous().GetAsync(photoPath);
        Assert.Equal(HttpStatusCode.Redirect, forVisitor.StatusCode);
        Assert.StartsWith("/cuenta/entrar", LocationPath(forVisitor));
        // (an unknown URL falls under the staff-only fallback policy, so visitors get the login redirect)
        var oldUrl = await Anonymous().GetAsync("/uploads/mascotas/" + fileName);
        Assert.NotEqual(HttpStatusCode.OK, oldUrl.StatusCode);
        Assert.NotEqual("image/webp", oldUrl.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/uploads/mascotas/" + fileName)).StatusCode);

        // The pets list uses the private URL for the avatar.
        Assert.Contains($"src=\"{photoPath}\"", await staff.GetStringAsync("/Pets"));
    }

    [Theory]
    [InlineData("/fotos/mascotas/no-existe.webp")]
    [InlineData("/fotos/mascotas/0123456789abcdef0123456789abcdef.png")]
    [InlineData("/fotos/mascotas/0123456789abcdef0123456789abcdef.webp")]
    [InlineData("/fotos/mascotas/..%2F..%2Fappsettings.json")]
    [InlineData("/fotos/mascotas/web.config")]
    public async Task Photo_endpoint_rejects_anything_that_is_not_a_stored_photo(string url)
    {
        var staff = await factory.CreateStaffClientAsync(allowAutoRedirect: false);

        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(url)).StatusCode);
    }

    // ─────────────── Headers ───────────────

    [Theory]
    [InlineData("/")]
    [InlineData("/css/site.css")]
    [InlineData("/cuenta/entrar")]
    public async Task Responses_carry_the_security_headers(string url)
    {
        var response = await Anonymous().GetAsync(url);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("frame-src https://www.openstreetmap.org", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("script-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com;", csp);
        Assert.DoesNotContain("unsafe-eval", csp);
    }
}

/// <summary>The app with the production limits (5 posts per minute per IP).</summary>
public class RateLimitedWebFactory : MiMascotaWebFactory
{
    protected override Dictionary<string, string?> ExtraSettings => new()
    {
        ["RateLimits:LoginPerMinute"] = "5",
        ["RateLimits:ContactPerMinute"] = "5"
    };
}

public class RateLimitTests(RateLimitedWebFactory factory) : IClassFixture<RateLimitedWebFactory>
{
    [Fact]
    public async Task Sixth_login_post_in_a_minute_gets_a_spanish_429()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await MiMascotaWebFactory.LoginAsync(client, "nadie@mimascota.test", "NoEsLaClave1")).StatusCode);
        }

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.OK, s));
        var last = await MiMascotaWebFactory.LoginAsync(client, "nadie@mimascota.test", "NoEsLaClave1");
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.Contains("Demasiados intentos", await last.Content.ReadAsStringAsync());
        Assert.Equal("60", last.Headers.GetValues("Retry-After").Single());

        // Reading the login page is not limited.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/cuenta/entrar")).StatusCode);
    }

    [Fact]
    public async Task Sixth_contact_message_in_a_minute_gets_a_spanish_429()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await MiMascotaWebFactory.GetAntiforgeryTokenAsync(client, "/contacto");
        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
        {
            last = await client.PostAsync("/contacto", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Name"] = "Spam",
                ["Contact"] = "spam@example.com",
                ["Message"] = $"Mensaje repetido número {i}."
            }));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Contains("Espera un minuto", await last.Content.ReadAsStringAsync());
        Assert.Equal(5, factory.WithDb(db => db.ContactMessages.Count(m => m.Name == "Spam")));
    }
}

/// <summary>Outside Development no demo account is created; the first admin comes from configuration.</summary>
public class ProductionSeedTests
{
    private sealed class ProductionFactory(Dictionary<string, string?> settings) : MiMascotaWebFactory
    {
        protected override string Environment => "Production";
        protected override Dictionary<string, string?> ExtraSettings => settings;
    }

    [Fact]
    public async Task No_demo_accounts_outside_development()
    {
        await using var app = new ProductionFactory([]);
        _ = app.Server; // start the app (runs the seeding)

        Assert.Null(await app.WithUsersAsync(users => users.FindByEmailAsync(StaffSeeder.DemoAdminEmail)));
        Assert.Null(await app.WithUsersAsync(users => users.FindByEmailAsync(StaffSeeder.DemoReceptionEmail)));
        Assert.Equal(0, app.WithDb(db => db.Users.Count()));
        Assert.Equal(2, app.WithDb(db => db.Roles.Count()));
    }

    [Fact]
    public async Task First_admin_comes_from_configuration()
    {
        await using var app = new ProductionFactory(new()
        {
            ["Admin:Email"] = "duena@mimascota.test",
            ["Admin:Password"] = "UnaClaveLarga2026"
        });
        _ = app.Server;

        var (exists, isAdmin, validPassword) = await app.WithUsersAsync(async users =>
        {
            var user = await users.FindByEmailAsync("duena@mimascota.test");
            return (user is not null,
                    user is not null && await users.IsInRoleAsync(user, Roles.Admin),
                    user is not null && await users.CheckPasswordAsync(user, "UnaClaveLarga2026"));
        });
        Assert.True(exists);
        Assert.True(isAdmin);
        Assert.True(validPassword);
        Assert.Equal(1, app.WithDb(db => db.Users.Count()));
    }

    [Fact]
    public async Task Login_cookie_is_secure_outside_development()
    {
        await using var app = new ProductionFactory(new()
        {
            ["Admin:Email"] = "segura@mimascota.test",
            ["Admin:Password"] = "UnaClaveLarga2026"
        });
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var response = await MiMascotaWebFactory.LoginAsync(client, "segura@mimascota.test", "UnaClaveLarga2026");

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("MiMascota.Personal=", StringComparison.Ordinal));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
