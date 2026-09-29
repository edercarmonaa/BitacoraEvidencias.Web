using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Security;

public static class ApplicationUserSecurityRefreshExtensions
{
    public static void UseApplicationUserSecurityRefresh(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated != true ||
                IsPasswordChangeBypassPath(context.Request.Path))
            {
                await next();
                return;
            }

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                await next();
                return;
            }

            var dbContext = context.RequestServices.GetRequiredService<AppDbContext>();
            var securityState = await dbContext.UsuariosSistema
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new
                {
                    x.Activo,
                    x.MustChangePassword,
                    x.Usuario,
                    x.Rol
                })
                .SingleOrDefaultAsync();

            if (securityState is null || !securityState.Activo)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/Login");
                return;
            }

            var currentUsername = context.User.Identity?.Name;
            var currentRole = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.Equals(currentUsername, securityState.Usuario, StringComparison.Ordinal) ||
                !string.Equals(currentRole, securityState.Rol, StringComparison.Ordinal))
            {
                var refreshedPrincipal = BuildApplicationPrincipal(userId, securityState.Usuario, securityState.Rol);
                context.User = refreshedPrincipal;
                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    refreshedPrincipal);
            }

            if (!securityState.MustChangePassword)
            {
                await next();
                return;
            }

            var returnUrl = $"{context.Request.Path}{context.Request.QueryString}";
            var targetUrl = $"/Account/ChangePassword?returnUrl={Uri.EscapeDataString(returnUrl)}";
            context.Response.Redirect(targetUrl);
        });
    }

    private static bool IsPasswordChangeBypassPath(PathString path)
    {
        return path.StartsWithSegments("/Account/ChangePassword", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/Login", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/Logout", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase);
    }

    private static ClaimsPrincipal BuildApplicationPrincipal(int userId, string username, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role)
        };

        return new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
