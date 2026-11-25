using System.Security.Claims;

namespace MiMascota.Web.Security;

/// <summary>The two staff roles. Names are in Spanish because they are shown in the UI as they are.</summary>
public static class Roles
{
    /// <summary>Everything, plus managing staff accounts and the services catalog.</summary>
    public const string Admin = "Administrador";

    /// <summary>Appointments, owners, pets and the contact inbox. Services are read-only.</summary>
    public const string Reception = "Recepción";

    /// <summary>Comma-separated list for [Authorize(Roles = ...)]: any staff member.</summary>
    public const string AnyStaff = Admin + "," + Reception;

    public static readonly string[] All = [Admin, Reception];
}

public static class StaffUserExtensions
{
    /// <summary>A logged-in clinic employee (Administrador or Recepción).</summary>
    public static bool IsStaff(this ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true && (user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Reception));

    public static bool IsAdmin(this ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true && user.IsInRole(Roles.Admin);

    /// <summary>The role shown under the user's name in the menu.</summary>
    public static string RoleLabel(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) ? Roles.Admin : user.IsInRole(Roles.Reception) ? Roles.Reception : "Sin rol";
}
