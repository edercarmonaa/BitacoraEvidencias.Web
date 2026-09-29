using System.Text;

namespace BitacoraEvidencias.Web.Services;

public static class OficioStoragePath
{
    public static string ToFolderName(string numeroOficio)
    {
        if (numeroOficio is null)
        {
            throw new ArgumentNullException(nameof(numeroOficio));
        }

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(numeroOficio.Length);

        foreach (var ch in numeroOficio.Trim())
        {
            if (invalid.Contains(ch))
            {
                continue;
            }

            builder.Append(char.IsWhiteSpace(ch) ? '_' : ch);
        }

        var cleaned = builder.ToString();
        return string.IsNullOrWhiteSpace(cleaned) ? "sin_oficio" : cleaned;
    }

    public static bool TryFindFolderCollision(
        string numeroOficio,
        IEnumerable<string> existingNumerosOficio,
        out string? conflictingNumeroOficio)
    {
        var targetFolder = ToFolderName(numeroOficio);

        foreach (var existingNumero in existingNumerosOficio)
        {
            if (string.Equals(existingNumero, numeroOficio, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(ToFolderName(existingNumero), targetFolder, StringComparison.OrdinalIgnoreCase))
            {
                conflictingNumeroOficio = existingNumero;
                return true;
            }
        }

        conflictingNumeroOficio = null;
        return false;
    }

    public static bool TryFindAnyFolderCollision(
        IEnumerable<string> numerosOficio,
        out string? numeroOficio,
        out string? conflictingNumeroOficio)
    {
        var folderOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var numero in numerosOficio)
        {
            var folderName = ToFolderName(numero);
            if (folderOwners.TryGetValue(folderName, out var existingNumero) &&
                !string.Equals(existingNumero, numero, StringComparison.Ordinal))
            {
                numeroOficio = numero;
                conflictingNumeroOficio = existingNumero;
                return true;
            }

            folderOwners.TryAdd(folderName, numero);
        }

        numeroOficio = null;
        conflictingNumeroOficio = null;
        return false;
    }
}
