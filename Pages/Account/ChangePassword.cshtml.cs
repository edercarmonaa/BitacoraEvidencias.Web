using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages.Account;

[Authorize(Roles = "Admin,Capturista")]
public class ChangePasswordModel(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? FlashSuccess { get; set; }

    public bool RequirePasswordChange { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Login");
        }

        RequirePasswordChange = user.MustChangePassword;
        if (!RequirePasswordChange && string.IsNullOrWhiteSpace(returnUrl))
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Login");
        }

        RequirePasswordChange = user.MustChangePassword;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!PasswordHasher.VerifyPassword(Input.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            ModelState.AddModelError(nameof(Input.CurrentPassword), "La contraseña actual no es correcta.");
            return Page();
        }

        var (newHash, newSalt) = PasswordHasher.HashPassword(Input.NewPassword);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.MustChangePassword = false;
        user.PasswordUpdatedAtUtc = DateTime.UtcNow;
        user.IntentosFallidos = 0;
        user.BloqueadoHastaUtc = null;

        await dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Usuario),
            new(ClaimTypes.Role, user.Rol)
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Update,
            EntityType = "Usuario",
            EntityId = user.Id,
            Username = user.Usuario,
            Role = user.Rol,
            Details = "Usuario actualizó su contraseña.",
            Success = true
        });

        FlashSuccess = "Contraseña actualizada correctamente.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Index");
    }

    private async Task<UsuarioSistema?> GetCurrentUserAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return await dbContext.UsuariosSistema
            .FirstOrDefaultAsync(x => x.Id == userId && x.Activo);
    }

    public class InputModel
    {
        [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [StringLength(128, MinimumLength = 10, ErrorMessage = "La nueva contraseña debe tener al menos 10 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirma la nueva contraseña.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "La confirmación no coincide.")]
        [Display(Name = "Confirmar nueva contraseña")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

