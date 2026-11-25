using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Models;
using MiMascota.Web.Security;
using MiMascota.Web.ViewModels;

namespace MiMascota.Web.Controllers;

/// <summary>/personal: the administrator lists staff accounts, creates them and turns them on or off.</summary>
[Authorize(Roles = Roles.Admin)]
public class StaffController(UserManager<ApplicationUser> users, TimeProvider time) : Controller
{
    /// <summary>Lockout end used to switch an account off (Identity's lockout, but with no end).</summary>
    private static readonly DateTimeOffset DisabledUntil = DateTimeOffset.MaxValue;

    [HttpGet("personal")]
    public async Task<IActionResult> Index() => View(await BuildPageAsync(new NewStaffViewModel()));

    [HttpPost("personal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = nameof(StaffPageViewModel.NewStaff))] NewStaffViewModel form)
    {
        if (!Roles.All.Contains(form.Role))
        {
            ModelState.AddModelError("NewStaff.Role", "Elige un rol válido.");
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildPageAsync(form));
        }

        var email = form.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = form.FullName.Trim(),
            MustChangePassword = true
        };

        var created = await users.CreateAsync(user, form.TemporaryPassword);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
            {
                var field = error.Code.StartsWith("Password", StringComparison.Ordinal) ? "NewStaff.TemporaryPassword" : "NewStaff.Email";
                ModelState.AddModelError(field, error.Description);
            }
            form.TemporaryPassword = string.Empty;
            return View(nameof(Index), await BuildPageAsync(form));
        }

        await users.AddToRoleAsync(user, form.Role);
        TempData["Success"] = $"Se creó la cuenta de {user.FullName} ({form.Role}). Al entrar por primera vez deberá cambiar la contraseña temporal.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("personal/{id}/desactivar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == users.GetUserId(User))
        {
            TempData["Error"] = "No puedes desactivar tu propia cuenta.";
            return RedirectToAction(nameof(Index));
        }

        await users.SetLockoutEnabledAsync(user, true);
        await users.SetLockoutEndDateAsync(user, DisabledUntil);
        // New security stamp: the person's open sessions end at the next check (within a minute).
        await users.UpdateSecurityStampAsync(user);

        TempData["Success"] = $"Se desactivó la cuenta de {user.FullName}. Ya no puede entrar al sistema.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("personal/{id}/activar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);

        TempData["Success"] = $"Se activó la cuenta de {user.FullName}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<StaffPageViewModel> BuildPageAsync(NewStaffViewModel form)
    {
        var now = time.GetUtcNow();
        var currentId = users.GetUserId(User);
        var all = await users.Users.AsNoTracking().ToListAsync();

        var members = new List<StaffMember>();
        foreach (var user in all.OrderBy(u => u.FullName, StringComparer.CurrentCultureIgnoreCase))
        {
            var roles = await users.GetRolesAsync(user);
            var state = user.LockoutEnd is { } end && end > now
                ? (end > now.AddYears(100) ? StaffState.Disabled : StaffState.LockedOut)
                : StaffState.Active;
            members.Add(new StaffMember(user.Id, user.FullName, user.Email ?? user.UserName ?? string.Empty,
                roles.FirstOrDefault() ?? "Sin rol", state, user.LockoutEnd, user.MustChangePassword, user.Id == currentId));
        }

        return new StaffPageViewModel { Members = members, NewStaff = form };
    }
}
