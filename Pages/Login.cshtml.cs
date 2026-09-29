using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Pages;

[AllowAnonymous]
public class LoginModel(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetSafeReturnUrl(returnUrl));
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var nowUtc = DateTime.UtcNow;
        var username = NormalizeUserName(Input.Usuario);

        var user = await dbContext.UsuariosSistema
            .SingleOrDefaultAsync(x => x.UsuarioNormalizado == username && x.Activo);

        if (user is null)
        {
            await auditLogService.WriteAsync(new AuditLogEntry
            {
                EventType = AuditEvents.LoginFail,
                EntityType = "Auth",
                Username = Input.Usuario,
                Details = "Usuario inexistente o inactivo.",
                Success = false
            });
            ErrorMessage = "Usuario o contraseña incorrectos.";
            return Page();
        }

        if (user.BloqueadoHastaUtc.HasValue && user.BloqueadoHastaUtc.Value > nowUtc)
        {
            var remaining = user.BloqueadoHastaUtc.Value - nowUtc;
            var remainingMinutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            await auditLogService.WriteAsync(new AuditLogEntry
            {
                EventType = AuditEvents.Lockout,
                EntityType = "Auth",
                UserId = user.Id,
                Username = user.Usuario,
                Role = user.Rol,
                Details = $"Cuenta bloqueada. Restan {remainingMinutes} minuto(s).",
                Success = false
            });
            ErrorMessage = $"Cuenta bloqueada. Intenta de nuevo en {remainingMinutes} minuto(s).";
            return Page();
        }

        if (!PasswordHasher.VerifyPassword(Input.Contrasena, user.PasswordHash, user.PasswordSalt))
        {
            user.IntentosFallidos += 1;
            if (user.IntentosFallidos >= SecurityDefaults.MaxFailedAttempts)
            {
                user.IntentosFallidos = 0;
                user.BloqueadoHastaUtc = nowUtc.Add(SecurityDefaults.LockoutDuration);
            }

            await dbContext.SaveChangesAsync();
            await auditLogService.WriteAsync(new AuditLogEntry
            {
                EventType = AuditEvents.LoginFail,
                EntityType = "Auth",
                UserId = user.Id,
                Username = user.Usuario,
                Role = user.Rol,
                Details = "Contraseña incorrecta.",
                Success = false
            });

            if (user.BloqueadoHastaUtc.HasValue)
            {
                await auditLogService.WriteAsync(new AuditLogEntry
                {
                    EventType = AuditEvents.Lockout,
                    EntityType = "Auth",
                    UserId = user.Id,
                    Username = user.Usuario,
                    Role = user.Rol,
                    Details = "Cuenta bloqueada por intentos fallidos consecutivos.",
                    Success = false
                });
            }

            ErrorMessage = "Usuario o contraseña incorrectos.";
            return Page();
        }

        user.IntentosFallidos = 0;
        user.BloqueadoHastaUtc = null;
        user.UltimoAccesoUtc = nowUtc;
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
            EventType = AuditEvents.Login,
            EntityType = "Auth",
            UserId = user.Id,
            Username = user.Usuario,
            Role = user.Rol,
            Details = user.MustChangePassword
                ? "Inicio de sesión correcto con cambio de contraseña requerido."
                : "Inicio de sesión correcto.",
            Success = true
        });

        if (user.MustChangePassword)
        {
            var safeReturnUrl = GetSafeReturnUrl(returnUrl);
            return RedirectToPage("/Account/ChangePassword", new { returnUrl = safeReturnUrl });
        }

        return LocalRedirect(GetSafeReturnUrl(returnUrl));
    }

    private string GetSafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return Url.Page("/Index") ?? "/";
    }

    private static string NormalizeUserName(string username)
    {
        return username.Trim().ToUpperInvariant();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "El usuario es obligatorio.")]
        [StringLength(60)]
        [Display(Name = "Usuario")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(128)]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; } = string.Empty;
    }
}

