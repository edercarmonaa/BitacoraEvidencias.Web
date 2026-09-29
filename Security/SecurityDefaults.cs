namespace BitacoraEvidencias.Web.Security;

public static class SecurityDefaults
{
    public const string AdminRole = "Admin";
    public const string CapturistaRole = "Capturista";
    public const string AppAccessPolicy = "AppAccess";

    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromMinutes(30);
}
