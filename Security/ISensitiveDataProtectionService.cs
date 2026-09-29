namespace BitacoraEvidencias.Web.Security;

public interface ISensitiveDataProtectionService
{
    bool IsConfigured { get; }
    bool IsProtected(string? value);
    string? ProtectString(string? value);
    string? UnprotectString(string? value);
    string? BlindIndex(string? value);
    string? Suffix(string? value, int length = 4);
    byte[] ProtectBytes(byte[] plaintext);
    byte[] UnprotectBytes(byte[] protectedBytes);
    bool IsProtectedPayload(ReadOnlySpan<byte> payload);
}
