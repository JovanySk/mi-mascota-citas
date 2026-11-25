using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using MiMascota.Web.Data;
using MiMascota.Web.Models;

namespace MiMascota.Web.Security;

/// <summary>ASP.NET Core Identity for the staff accounts, the login cookie and the authorization policies.</summary>
public static class StaffSecuritySetup
{
    public const string LoginPath = "/cuenta/entrar";
    public const string LogoutPath = "/cuenta/salir";
    public const string AccessDeniedPath = "/cuenta/acceso-denegado";

    /// <summary>How long the lockout after 5 failed passwords lasts.</summary>
    public static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(15);
    public const int MaxFailedAttempts = 5;

    public static IServiceCollection AddStaffSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.MaxFailedAccessAttempts = MaxFailedAttempts;
                options.Lockout.DefaultLockoutTimeSpan = LockoutTime;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddPasswordValidator<LetterPasswordValidator<ApplicationUser>>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>()
            .AddClaimsPrincipalFactory<StaffClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "MiMascota.Personal";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = LoginPath;
            options.LogoutPath = LogoutPath;
            options.AccessDeniedPath = AccessDeniedPath;
        });

        // A disabled account (or a changed password) ends its open sessions within a minute.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddAuthorization(options =>
        {
            // Everything requires a staff member unless the endpoint says [AllowAnonymous].
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(Roles.All)
                .Build();
        });

        // Containers lose the default key folder on restart (everyone would be logged out):
        // "DataProtection:KeysPath" points the keys to a persistent volume.
        if (configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        {
            services.AddDataProtection()
                .SetApplicationName("MiMascota")
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        return services;
    }
}
