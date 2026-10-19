using BitacoraEvidencias.Web.Security;
using Microsoft.Extensions.Options;

namespace BitacoraEvidencias.Web.Tests.Services;

// Each fixture owns its variable; never read or replace the application's real key.
internal sealed class StorageProtectionFixture : IDisposable
{
    private readonly string _variable = "BITACORA_STORAGE_TEST_" + Guid.NewGuid().ToString("N");

    public StorageProtectionFixture(bool enabled)
    {
        if (enabled)
            Environment.SetEnvironmentVariable(_variable, Convert.ToBase64String(new byte[32]));

        Service = new SensitiveDataProtectionService(Options.Create(new SensitiveDataProtectionOptions
        {
            KeyEnvironmentVariable = _variable
        }));
    }

    public SensitiveDataProtectionService Service { get; }

    public string RelativePath(string legacyPath)
    {
        if (!Service.IsConfigured) return legacyPath;
        var parts = legacyPath.Split('/');
        if (parts[0].StartsWith("of_", StringComparison.Ordinal)) return legacyPath;
        parts[0] = "of_" + Service.BlindIndex(parts[0])![..32];
        return string.Join('/', parts);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_variable, null);
}
