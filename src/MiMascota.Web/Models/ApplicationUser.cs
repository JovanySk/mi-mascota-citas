using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace MiMascota.Web.Models;

/// <summary>A clinic staff account (ASP.NET Core Identity user). Pet owners never log in.</summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Name shown in the user menu and in the staff list.</summary>
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>True for accounts created with a temporary password: after logging in they are sent to change it.</summary>
    public bool MustChangePassword { get; set; }
}
