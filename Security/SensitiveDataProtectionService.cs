using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace BitacoraEvidencias.Web.Security;

public sealed class SensitiveDataProtectionService(
    IOptions<SensitiveDataProtectionOptions> options) : ISensitiveDataProtectionService
{
    private const string StringPrefix = "enc:v1:";
    private static readonly byte[] FilePrefix = "BITACORA-ENC-V1"u8.ToArray();
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly Lazy<KeyMaterial?> _keyMaterial = new(() => LoadKeyMaterial(options.Value));

    public bool IsConfigured => _keyMaterial.Value is not null;

    public bool IsProtected(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.StartsWith(StringPrefix, StringComparison.Ordinal);

    public string? ProtectString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (IsProtected(value))
        {
            return value;
        }

        var material = RequireKeyMaterial();
        var plaintext = Encoding.UTF8.GetBytes(value);
        var payload = ProtectPayload(plaintext, material.EncryptionKey);
        return StringPrefix + Convert.ToBase64String(payload);
    }

    public string? UnprotectString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!IsProtected(value))
        {
            return value;
        }

        var material = RequireKeyMaterial();
        var payload = Convert.FromBase64String(value[StringPrefix.Length..]);
        var plaintext = UnprotectPayload(payload, material.EncryptionKey);
        return Encoding.UTF8.GetString(plaintext);
    }

    public string? BlindIndex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var material = RequireKeyMaterial();
        var normalized = NormalizeForIndex(UnprotectString(value));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        using var hmac = new HMACSHA256(material.HmacKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized)));
    }

    public string? Suffix(string? value, int length = 4)
    {
        var plaintext = UnprotectString(value)?.Trim();
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        var safeLength = Math.Clamp(length, 1, plaintext.Length);
        return plaintext[^safeLength..];
    }

    public byte[] ProtectBytes(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (IsProtectedPayload(plaintext))
        {
            return plaintext;
        }

        var payload = ProtectPayload(plaintext, RequireKeyMaterial().EncryptionKey);
        var output = new byte[FilePrefix.Length + payload.Length];
        FilePrefix.CopyTo(output, 0);
        payload.CopyTo(output.AsSpan(FilePrefix.Length));
        return output;
    }

    public byte[] UnprotectBytes(byte[] protectedBytes)
    {
        ArgumentNullException.ThrowIfNull(protectedBytes);
        if (!IsProtectedPayload(protectedBytes))
        {
            return protectedBytes;
        }

        return UnprotectPayload(protectedBytes.AsSpan(FilePrefix.Length).ToArray(), RequireKeyMaterial().EncryptionKey);
    }

    public bool IsProtectedPayload(ReadOnlySpan<byte> payload)
        => payload.Length > FilePrefix.Length && payload[..FilePrefix.Length].SequenceEqual(FilePrefix);

    private static byte[] ProtectPayload(ReadOnlySpan<byte> plaintext, byte[] encryptionKey)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(encryptionKey, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload.AsSpan(NonceSize));
        ciphertext.CopyTo(payload.AsSpan(NonceSize + TagSize));
        return payload;
    }

    private static byte[] UnprotectPayload(ReadOnlySpan<byte> payload, byte[] encryptionKey)
    {
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("El contenido cifrado no tiene un formato valido.");
        }

        var nonce = payload[..NonceSize];
        var tag = payload.Slice(NonceSize, TagSize);
        var ciphertext = payload[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(encryptionKey, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    private static string NormalizeForIndex(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private KeyMaterial RequireKeyMaterial()
        => _keyMaterial.Value ?? throw new InvalidOperationException(
            $"Configura la variable de entorno {SensitiveDataProtectionOptions.DefaultEnvironmentVariableName} antes de usar cifrado de datos sensibles.");

    private static KeyMaterial? LoadKeyMaterial(SensitiveDataProtectionOptions options)
    {
        var variableName = string.IsNullOrWhiteSpace(options.KeyEnvironmentVariable)
            ? SensitiveDataProtectionOptions.DefaultEnvironmentVariableName
            : options.KeyEnvironmentVariable.Trim();

        var rawKey = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return null;
        }

        var masterKey = TryDecodeBase64(rawKey.Trim(), out var decoded)
            ? decoded
            : Encoding.UTF8.GetBytes(rawKey.Trim());

        if (masterKey.Length < 32)
        {
            // If the provided key is shorter than 32 bytes, derive a 32-byte master key
            // using SHA-256 to ensure sufficient length while preserving entropy.
            masterKey = SHA256.HashData(masterKey);
        }

        return new KeyMaterial(
            DeriveKey(masterKey, "bitacora:data-encryption:v1"),
            DeriveKey(masterKey, "bitacora:blind-index:v1"));
    }

    private static byte[] DeriveKey(byte[] masterKey, string purpose)
        => HMACSHA256.HashData(masterKey, Encoding.UTF8.GetBytes(purpose));

    private static bool TryDecodeBase64(string value, out byte[] decoded)
    {
        try
        {
            decoded = Convert.FromBase64String(value);
            return decoded.Length > 0;
        }
        catch (FormatException)
        {
            decoded = Array.Empty<byte>();
            return false;
        }
    }

    private sealed record KeyMaterial(byte[] EncryptionKey, byte[] HmacKey);
}
