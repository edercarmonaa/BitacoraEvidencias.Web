using BitacoraEvidencias.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitacoraEvidencias.Web.Services;

public static class ServiceCollectionExtensions
{
    public static void AddApplicationRazorPages(this IServiceCollection services)
    {
        services.AddRazorPages(options =>
        {
            options.Conventions.AllowAnonymousToPage("/Login");
            options.Conventions.AllowAnonymousToPage("/Error");
        });
    }

    public static void AddApplicationAuthentication(this IServiceCollection services, bool requireHttpsCookies)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/Error";
                options.ExpireTimeSpan = SecurityDefaults.SessionIdleTimeout;
                options.SlidingExpiration = true;
                options.Cookie.Name = "Bitacora.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = requireHttpsCookies
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
            });
    }

    public static void AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            var appAccessPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireRole(SecurityDefaults.AdminRole, SecurityDefaults.CapturistaRole)
                .Build();

            options.AddPolicy(SecurityDefaults.AppAccessPolicy, appAccessPolicy);
            options.FallbackPolicy = appAccessPolicy;
        });
    }

    public static void AddValidatedFileStorageOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RootPath),
                "Storage:RootPath es obligatorio.")
            .Validate(
                options =>
                {
                    if (string.IsNullOrWhiteSpace(options.RequestPath))
                    {
                        return false;
                    }

                    return options.RequestPath.StartsWith('/') &&
                           !options.RequestPath.Contains(' ') &&
                           !options.RequestPath.Contains("//", StringComparison.Ordinal) &&
                           (options.RequestPath.Length == 1 || !options.RequestPath.EndsWith('/'));
                },
                "Storage:RequestPath es obligatorio, debe iniciar con '/', no debe contener espacios ni '//' y no debe terminar con '/' salvo que sea '/'.")
            .Validate(
                options => options.ThumbnailWidth > 0,
                "Storage:ThumbnailWidth debe ser mayor que cero.")
            .Validate(
                options => options.ThumbnailHeight > 0,
                "Storage:ThumbnailHeight debe ser mayor que cero.")
            .Validate(
                options => options.MaxUploadMegabytes > 0,
                "Storage:MaxUploadMegabytes debe ser mayor que cero.")
            .Validate(
                options => options.MaxImageDimension > 0,
                "Storage:MaxImageDimension debe ser mayor que cero.")
            .Validate(
                options => options.JpegQuality is >= 1 and <= 100,
                "Storage:JpegQuality debe estar entre 1 y 100.")
            .ValidateOnStart();
    }

    public static void AddSensitiveDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SensitiveDataProtectionOptions>(
            configuration.GetSection(SensitiveDataProtectionOptions.SectionName));
        services.AddSingleton<ISensitiveDataProtectionService, SensitiveDataProtectionService>();
        services.AddScoped<SensitiveDataMigrationService>();
    }
}
