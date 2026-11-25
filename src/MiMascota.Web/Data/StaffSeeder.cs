using Microsoft.AspNetCore.Identity;
using MiMascota.Web.Models;
using MiMascota.Web.Security;

namespace MiMascota.Web.Data;

/// <summary>
/// Creates the roles and the first staff accounts.
///
/// - Development: two demo accounts with known passwords (documented in the README), unless
///   "Auth:SeedDemoStaff" is false. They use the reserved ".test" domain.
/// - Any environment: if "Admin:Email" and "Admin:Password" are configured (e.g. the environment
///   variables Admin__Email / Admin__Password) and that account does not exist yet, it is created
///   as Administrador. Outside Development no default password is ever created.
/// </summary>
public static class StaffSeeder
{
    public const string DemoAdminEmail = "admin@mimascota.test";
    public const string DemoAdminPassword = "Admin123!";
    public const string DemoReceptionEmail = "recepcion@mimascota.test";
    public const string DemoReceptionPassword = "Recepcion123!";

    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment, IConfiguration configuration, ILogger logger)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (environment.IsDevelopment() && configuration.GetValue("Auth:SeedDemoStaff", true))
        {
            await EnsureUserAsync(users, logger, DemoAdminEmail, DemoAdminPassword, "Administración (demo)", Roles.Admin);
            await EnsureUserAsync(users, logger, DemoReceptionEmail, DemoReceptionPassword, "Recepción (demo)", Roles.Reception);
        }

        var adminEmail = configuration["Admin:Email"];
        var adminPassword = configuration["Admin:Password"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            await EnsureUserAsync(users, logger, adminEmail.Trim(), adminPassword,
                configuration["Admin:FullName"] ?? "Administración", Roles.Admin);
        }
        else if (!environment.IsDevelopment() && (await users.GetUsersInRoleAsync(Roles.Admin)).Count == 0)
        {
            logger.LogWarning(
                "There is no Administrador account. Set Admin__Email and Admin__Password (environment variables) " +
                "and restart the app to create the first one.");
        }
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, ILogger logger,
        string email, string password, string fullName, string role)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            return; // never overwrite an existing account (or its password)
        }

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogError("Could not create the staff account {Email}: {Errors}",
                email, string.Join(" ", created.Errors.Select(e => e.Description)));
            return;
        }

        await users.AddToRoleAsync(user, role);
        logger.LogInformation("Created the {Role} account {Email}.", role, email);
    }
}
