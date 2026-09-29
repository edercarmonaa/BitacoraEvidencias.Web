using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BitacoraEvidencias.Web.Services;

public static class ImportParsing
{
    private static readonly string[] SupportedDateFormats =
    [
        "yyyy-MM-dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "MM/dd/yyyy",
        "M/d/yyyy"
    ];

    private static readonly Regex HeaderCleanupPattern = new("[^a-z0-9]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return HeaderCleanupPattern.Replace(builder.ToString(), string.Empty);
    }

    public static bool TryParseRequiredDate(string? rawValue, out DateTime parsedDate)
    {
        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
        {
            try
            {
                parsedDate = DateTime.FromOADate(serial).Date;
                return true;
            }
            catch
            {
                // Ignore and continue with string parsing.
            }
        }

        if (DateTime.TryParseExact(
            rawValue,
            SupportedDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out parsedDate))
        {
            parsedDate = parsedDate.Date;
            return true;
        }

        if (DateTime.TryParse(
            rawValue,
            CultureInfo.GetCultureInfo("es-MX"),
            DateTimeStyles.AllowWhiteSpaces,
            out parsedDate))
        {
            parsedDate = parsedDate.Date;
            return true;
        }

        parsedDate = default;
        return false;
    }

    public static bool TryParseRequiredYear(string? rawValue, out int year)
    {
        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out year) &&
            year is >= 1900 and <= 2100)
        {
            return true;
        }

        year = 0;
        return false;
    }

    public static bool TryParseOptionalHttpUrl(string? rawValue, out string? normalizedUrl)
    {
        normalizedUrl = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        var trimmed = rawValue.Trim();
        if (trimmed.Length > 1000)
        {
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedUrl = trimmed;
        return true;
    }

    public static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
