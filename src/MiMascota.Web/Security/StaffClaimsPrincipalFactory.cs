using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MiMascota.Web.Models;

namespace MiMascota.Web.Security;

public static class StaffClaims
{
    /// <summary>The staff member's name, shown in the user menu without a database query per page.</summary>
    public const string FullName = "mimascota:full_name";
}

/// <summary>Adds the full name to the login cookie (roles are added by the base class).</summary>
public class StaffClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            identity.AddClaim(new Claim(StaffClaims.FullName, user.FullName));
        }
        return identity;
    }
}
