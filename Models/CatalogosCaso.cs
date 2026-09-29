using System.ComponentModel.DataAnnotations;

namespace BitacoraEvidencias.Web.Models;

public static class CatalogosCaso
{
    public const string EstatusPendiente = "Pendiente";
    public const string EstatusEnProceso = "En proceso";
    public const string EstatusCompletado = "Completado";
    public const string EstatusRechazado = "Rechazado";

    public static readonly IReadOnlyList<string> Estatus = new[]
    {
        EstatusPendiente,
        EstatusEnProceso,
        EstatusCompletado,
        EstatusRechazado
    };

    public static readonly IReadOnlyList<string> TiposEvidencia = new[]
    {
        "Oficio",
        "Lista",
        "Extra"
    };

    public static readonly IReadOnlyList<string> NivelesEducativos = new[]
    {
        "Primaria",
        "Secundaria"
    };

    private static readonly HashSet<string> EstatusSet = new(Estatus, StringComparer.Ordinal);
    private static readonly HashSet<string> TiposEvidenciaSet = new(TiposEvidencia, StringComparer.Ordinal);
    private static readonly HashSet<string> NivelesEducativosSet = new(NivelesEducativos, StringComparer.Ordinal);

    public static bool EsEstatusValido(string? value)
    {
        return ContainsTrimmed(EstatusSet, value);
    }

    public static bool EsTipoEvidenciaValido(string? value)
    {
        return ContainsTrimmed(TiposEvidenciaSet, value);
    }

    public static bool EsNivelEducativoValido(string? value)
    {
        return ContainsTrimmed(NivelesEducativosSet, value);
    }

    public static ValidationResult? ValidateEstatus(object? value, ValidationContext _)
    {
        return EsEstatusValido(value as string)
            ? ValidationResult.Success
            : new ValidationResult("Selecciona un estatus valido.");
    }

    public static ValidationResult? ValidateTipoEvidencia(object? value, ValidationContext _)
    {
        return EsTipoEvidenciaValido(value as string)
            ? ValidationResult.Success
            : new ValidationResult("Selecciona un tipo de evidencia valido.");
    }

    public static ValidationResult? ValidateNivelEducativo(object? value, ValidationContext _)
    {
        return EsNivelEducativoValido(value as string)
            ? ValidationResult.Success
            : new ValidationResult("Selecciona un nivel educativo valido.");
    }

    private static bool ContainsTrimmed(HashSet<string> values, string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && values.Contains(value.Trim());
    }
}
