using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BitacoraEvidencias.Web.Tests.Security;

internal sealed class TestCookieAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder),
      IAuthenticationSignInHandler,
      IAuthenticationSignOutHandler
{
    private const string UserIdHeader = "X-Test-UserId";
    private const string UsernameHeader = "X-Test-Username";
    private const string RoleHeader = "X-Test-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValues.ToString();
        var username = Request.Headers[UsernameHeader].ToString();
        var role = Request.Headers[RoleHeader].ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            claims.Add(new Claim(ClaimTypes.Name, username));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(
                principal,
                CookieAuthenticationDefaults.AuthenticationScheme)));
    }

    public Task SignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        Context.Items["TestSignInInvoked"] = true;
        Context.Items["TestSignedInName"] = user.Identity?.Name;
        Context.Items["TestSignedInRole"] = user.FindFirstValue(ClaimTypes.Role);
        return Task.CompletedTask;
    }

    public Task SignOutAsync(AuthenticationProperties? properties)
    {
        Context.Items["TestSignOutInvoked"] = true;
        return Task.CompletedTask;
    }
}
