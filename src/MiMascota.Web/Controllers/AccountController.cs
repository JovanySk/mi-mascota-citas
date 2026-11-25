using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiMascota.Web.Models;
using MiMascota.Web.Security;
using MiMascota.Web.ViewModels;

namespace MiMascota.Web.Controllers;

/// <summary>Staff login, logout, access denied and "change my password".</summary>
public class AccountController(
    SignInManager<ApplicationUser> signIn,
    UserManager<ApplicationUser> users,
    ILogger<AccountController> logger) : Controller
{
    public const string InvalidLogin = "Correo o contraseña incorrectos.";
    public const string LockedOutMessage =
        "Esta cuenta está bloqueada. Si la contraseña se escribió mal 5 veces, espera 15 minutos y vuelve a intentarlo; " +
        "si sigue bloqueada, pide ayuda al administrador.";

    [AllowAnonymous]
    [HttpGet("cuenta/entrar")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.IsStaff())
        {
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost("cuenta/entrar")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // User names are the e-mails. Every failure gets the same message, so the page never
        // tells whether an e-mail has an account.
        var email = model.Email.Trim();
        var result = await signIn.PasswordSignInAsync(email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await users.FindByNameAsync(email);
            logger.LogInformation("Staff login: {Email}.", email);
            if (user?.MustChangePassword == true)
            {
                TempData["Info"] = "Entraste con una contraseña temporal. Elige tu propia contraseña para continuar.";
                return RedirectToAction(nameof(ChangePassword));
            }
            return LocalRedirect(SafeReturnUrl(model.ReturnUrl));
        }

        if (result.IsLockedOut)
        {
            logger.LogWarning("Login attempt on a locked-out account: {Email}.", email);
            ModelState.AddModelError(string.Empty, LockedOutMessage);
        }
        else
        {
            ModelState.AddModelError(string.Empty, InvalidLogin);
        }

        model.Password = string.Empty;
        return View(model);
    }

    // POST only, with the antiforgery token: a link or an image on another site cannot log anyone out.
    [HttpPost("cuenta/salir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        TempData["Success"] = "Cerraste tu sesión. ¡Hasta pronto!";
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet("cuenta/acceso-denegado")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpGet("cuenta/contrasena")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost("cuenta/contrasena")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var field = error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)
                    ? nameof(ChangePasswordViewModel.CurrentPassword)
                    : nameof(ChangePasswordViewModel.NewPassword);
                ModelState.AddModelError(field, error.Description);
            }
            return View(new ChangePasswordViewModel());
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await users.UpdateAsync(user);
        }

        // The password change rotates the security stamp: refresh this session's cookie.
        await signIn.RefreshSignInAsync(user);
        TempData["Success"] = "Tu contraseña se cambió correctamente.";
        return RedirectToAction("Index", "Home");
    }

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Home")!;
}
