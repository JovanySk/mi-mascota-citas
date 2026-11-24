using System.Globalization;
using Microsoft.AspNetCore.Localization;
using MiMascota.Web.Data;
using MiMascota.Web.Options;
using MiMascota.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Spanish messages for model binding errors (e.g. a letter typed in a number field).
    var messages = options.ModelBindingMessageProvider;
    messages.SetValueMustNotBeNullAccessor(_ => "Este campo es obligatorio.");
    messages.SetMissingBindRequiredValueAccessor(field => $"Falta el valor de «{field}».");
    messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"El valor «{value}» no es válido para «{field}».");
    messages.SetValueMustBeANumberAccessor(field => $"El campo «{field}» debe ser un número.");
});

builder.Services.Configure<ClinicOptions>(builder.Configuration.GetSection(ClinicOptions.SectionName));
builder.Services.AddClinicDatabase();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();

var app = builder.Build();

await app.InitializeDatabaseAsync();

// Dates, currency and number formats in Mexican Spanish.
var spanish = new CultureInfo("es-MX");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(spanish),
    SupportedCultures = [spanish],
    SupportedUICultures = [spanish]
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Exposed so the integration tests can use WebApplicationFactory<Program>.
public partial class Program;
