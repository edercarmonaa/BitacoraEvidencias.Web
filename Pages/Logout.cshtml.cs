using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;

namespace BitacoraEvidencias.Web.Pages;

[Authorize]
public class LogoutModel(IAuditLogService auditLogService) : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Logout,
            EntityType = "Auth",
            Details = "Cierre de sesión solicitado por usuario autenticado.",
            Success = true
        });

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }

    public IActionResult OnGet()
    {
        return RedirectToPage("/Index");
    }
}

