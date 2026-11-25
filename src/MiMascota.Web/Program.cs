using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using MiMascota.Web.Data;
using MiMascota.Web.Options;
using MiMascota.Web.Security;
using MiMascota.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST/PUT/DELETE needs the antiforgery token, even if an action forgets [ValidateAntiForgeryToken].
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

    // Spanish messages for model binding errors (e.g. a letter typed in a number field).
    var messages = options.ModelBindingMessageProvider;
    messages.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    messages.SetMissingBindRequiredValueAccessor(field => $"Falta el valor de «{field}».");
    messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"El valor «{value}» no es válido para «{field}».");
    messages.SetValueMustBeANumberAccessor(field => $"El campo «{field}» debe ser un número.");
});

builder.Services.Configure<ClinicOptions>(builder.Configuration.GetSection(ClinicOptions.SectionName));
builder.Services.AddClinicDatabase();
builder.Services.AddStaffSecurity(builder.Configuration, builder.Environment);
builder.Services.AddFormRateLimits();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IClinicHours, ClinicHours>();
// Pet photos are private: App_Data/uploads/mascotas (outside wwwroot), served by PhotosController.
// "PetPhotos:Folder" (absolute, or relative to the content root) moves them, e.g. to a volume or a temp folder in tests.
builder.Services.AddSingleton<IPetPhotoStorage>(sp => new PetPhotoStorage(Path.GetFullPath(
    sp.GetRequiredService<IConfiguration>()["PetPhotos:Folder"] ?? PetPhotoStorage.DefaultFolder,
    sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath)));

var app = builder.Build();

// Photos saved by older versions under wwwroot/uploads/mascotas move to the private folder
// (only with the default folder, so tests with their own temp folder never touch real files).
if (app.Configuration["PetPhotos:Folder"] is null
    && app.Services.GetRequiredService<IPetPhotoStorage>() is PetPhotoStorage storage)
{
    storage.MoveLegacyPhotos(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads", "mascotas"));
}

await app.InitializeDatabaseAsync();

// Dates, currency and number formats in Mexican Spanish.
var spanish = new CultureInfo("es-MX");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(spanish),
    SupportedCultures = [spanish],
    SupportedUICultures = [spanish]
});

app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles(); // public: css, js, fonts, illustrations (no pet photos here)
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Exposed so the integration tests can use WebApplicationFactory<Program>.
public partial class Program;
