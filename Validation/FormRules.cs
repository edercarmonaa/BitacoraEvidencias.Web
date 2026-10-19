using System.Text.RegularExpressions;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Services;

namespace BitacoraEvidencias.Web.Validation;

public sealed record CaseFormValues(string? Curp, string? NombreCompleto, string? Cct,
    int? PeriodoInicio, int? PeriodoFin, DateTime? FechaVerificacion)
{
    public static CaseFormValues From(CasoCorreccion value) => new(value.Curp, value.NombreCompleto,
        value.Cct, value.PeriodoInicio, value.PeriodoFin, value.FechaVerificacion);
}

public static partial class FormRules
{
    // LAN deployments may use a UTC host; business dates use Mexico City.
    public static DateTime Today => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Mexico_City").Date;
    public const string CurpPattern = @"^[A-Z][AEIOUX][A-Z]{2}[0-9]{2}(0[1-9]|1[0-2])(0[1-9]|[12][0-9]|3[01])[HM](AS|BC|BS|CC|CL|CM|CS|CH|DF|DG|GT|GR|HG|JC|MC|MN|MS|NT|NL|OC|PL|QT|QR|SP|SL|SR|TC|TS|TL|VZ|YN|ZS|NE)[B-DF-HJ-NP-TV-Z]{3}[A-Z0-9][0-9]$";
    public const string CctPattern = @"^(?:[0-9]{2}[A-Z]{3}[0-9]{4}[A-Z]|[0-9]{3}-[A-Z0-9]{2})$";

    public static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    public static bool IsCct(string? value) => Regex.IsMatch(NormalizeCode(value) ?? "", CctPattern, RegexOptions.CultureInvariant);
    public static bool IsCurp(string? value)
    {
        var text = NormalizeCode(value) ?? "";
        if (!Regex.IsMatch(text, CurpPattern, RegexOptions.CultureInvariant)) return false;
        var year = int.Parse(text.AsSpan(4, 2)) + (char.IsDigit(text[16]) ? 1900 : 2000);
        var month = int.Parse(text.AsSpan(6, 2));
        var day = int.Parse(text.AsSpan(8, 2));
        return day <= DateTime.DaysInMonth(year, month);
    }

    public static IReadOnlyList<ApplicationValidationError> ValidateCase(CaseFormValues value, DateTime officeDate,
        CaseFormValues? original = null, DateTime? today = null)
    {
        var errors = new List<ApplicationValidationError>();
        bool Changed<T>(T current, T previous) => original is null || !EqualityComparer<T>.Default.Equals(current, previous);
        bool TextChanged(string? current, string? previous) => original is null ||
            !string.Equals(current?.Trim() ?? "", previous?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
        if (TextChanged(value.Curp, original?.Curp))
        {
            if (string.IsNullOrWhiteSpace(value.Curp)) errors.Add(new("Curp", "La CURP es obligatoria."));
            else if (!IsCurp(value.Curp)) errors.Add(new("Curp", "Captura una CURP con formato valido de 18 caracteres y fecha valida."));
        }
        if (TextChanged(value.Cct, original?.Cct))
        {
            if (string.IsNullOrWhiteSpace(value.Cct)) errors.Add(new("Cct", "La CCT es obligatoria."));
            else if (!IsCct(value.Cct)) errors.Add(new("Cct", "Captura una CCT como 30PBH0442H, 184-D2 o 327-DV."));
        }
        if (TextChanged(value.NombreCompleto, original?.NombreCompleto) && string.IsNullOrWhiteSpace(value.NombreCompleto))
            errors.Add(new("NombreCompleto", "El nombre completo es obligatorio."));
        if (Changed(value.PeriodoInicio, original?.PeriodoInicio) && (!value.PeriodoInicio.HasValue || value.PeriodoInicio < 1900 || value.PeriodoInicio > 2100))
            errors.Add(new("PeriodoInicio", "Captura el periodo de inicio entre 1900 y 2100."));
        if (Changed(value.PeriodoFin, original?.PeriodoFin) && (!value.PeriodoFin.HasValue || value.PeriodoFin < 1900 || value.PeriodoFin > 2100))
            errors.Add(new("PeriodoFin", "Captura el periodo de fin entre 1900 y 2100."));
        if ((Changed(value.PeriodoInicio, original?.PeriodoInicio) || Changed(value.PeriodoFin, original?.PeriodoFin))
            && value.PeriodoInicio.HasValue && value.PeriodoFin.HasValue && value.PeriodoFin < value.PeriodoInicio)
            errors.Add(new("PeriodoFin", "El periodo fin no puede ser menor al periodo inicio."));
        if (Changed(value.FechaVerificacion?.Date, original?.FechaVerificacion?.Date))
        {
            if (!value.FechaVerificacion.HasValue) errors.Add(new("FechaVerificacion", "La fecha de verificacion es obligatoria."));
            else if (value.FechaVerificacion.Value.Date > (today ?? Today).Date)
                errors.Add(new("FechaVerificacion", "La fecha de verificacion no puede ser futura."));
            else if (value.FechaVerificacion.Value.Date < officeDate.Date)
                errors.Add(new("FechaVerificacion", "La fecha de verificacion no puede ser anterior a la fecha del oficio."));
        }
        return errors;
    }

    public static IReadOnlyList<ApplicationValidationError> ValidateOfficeDate(DateTime date, DateTime? original = null,
        IEnumerable<DateTime?>? caseDates = null, DateTime? today = null)
    {
        if (original.HasValue && original.Value.Date == date.Date) return [];
        if (date == default) return [new("FechaOficio", "La fecha del oficio es obligatoria.")];
        if (date.Date > (today ?? Today).Date) return [new("FechaOficio", "La fecha del oficio no puede ser futura.")];
        if (caseDates?.Any(d => d.HasValue && d.Value.Date < date.Date) == true)
            return [new("FechaOficio", "La fecha del oficio no puede ser posterior a la verificacion de sus casos.")];
        return [];
    }
}
