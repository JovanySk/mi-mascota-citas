using Microsoft.AspNetCore.Identity;

namespace MiMascota.Web.Security;

/// <summary>
/// Identity can require a digit, but not "at least one letter" (only a lower-case or an upper-case one).
/// Together with RequireDigit this gives the rule "8+ characters, with letters and numbers".
/// </summary>
public class LetterPasswordValidator<TUser> : IPasswordValidator<TUser> where TUser : class
{
    public const string Code = "PasswordRequiresLetter";

    public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string? password) =>
        Task.FromResult(password is not null && password.Any(char.IsLetter)
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError { Code = Code, Description = "La contraseña debe tener al menos una letra." }));
}
