using System.ComponentModel.DataAnnotations;
using MiMascota.Web.Security;

namespace MiMascota.Web.ViewModels;

public class LoginViewModel
{
    [Display(Name = "Correo")]
    [Required(ErrorMessage = "Escribe tu correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Contraseña")]
    [Required(ErrorMessage = "Escribe tu contraseña.")]
    [DataType(DataType.Password)]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Recordarme en este equipo")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class ChangePasswordViewModel
{
    [Display(Name = "Contraseña actual")]
    [Required(ErrorMessage = "Escribe tu contraseña actual.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Display(Name = "Contraseña nueva")]
    [Required(ErrorMessage = "Escribe la contraseña nueva.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos {2} caracteres.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Display(Name = "Repite la contraseña nueva")]
    [Required(ErrorMessage = "Repite la contraseña nueva.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>The "Nuevo integrante" form on /personal.</summary>
public class NewStaffViewModel
{
    [Display(Name = "Nombre completo")]
    [Required(ErrorMessage = "Escribe el nombre.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre {2} y {1} caracteres.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Correo")]
    [Required(ErrorMessage = "Escribe el correo.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Rol")]
    [Required(ErrorMessage = "Elige el rol.")]
    public string Role { get; set; } = Roles.Reception;

    [Display(Name = "Contraseña temporal")]
    [Required(ErrorMessage = "Escribe una contraseña temporal.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos {2} caracteres.")]
    [DataType(DataType.Password)]
    public string TemporaryPassword { get; set; } = string.Empty;
}

public enum StaffState
{
    Active,
    LockedOut,   // 5 failed passwords: unlocks by itself
    Disabled     // turned off by an administrator
}

public record StaffMember(string Id, string FullName, string Email, string Role, StaffState State,
    DateTimeOffset? LockoutEnd, bool MustChangePassword, bool IsCurrentUser);

public class StaffPageViewModel
{
    public required IReadOnlyList<StaffMember> Members { get; init; }
    public NewStaffViewModel NewStaff { get; init; } = new();
}
