namespace BitacoraEvidencias.Web.Security;

public sealed class SensitiveDataProtectionOptions
{
    public const string SectionName = "SensitiveDataProtection";
    public const string DefaultEnvironmentVariableName = "BITACORA_DATA_PROTECTION_KEY";

    public string KeyEnvironmentVariable { get; set; } = DefaultEnvironmentVariableName;
}
