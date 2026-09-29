using System.ComponentModel.DataAnnotations;

namespace BitacoraEvidencias.Web.Models;

public static class CatalogosAuditoriaCaso
{
    public const string Pendiente = "Pendiente";
    public const string Coincidente = "Coincidente";
    public const string NoCoincidente = "No coincidente";

    public static readonly IReadOnlyList<string> Estados = new[]
    {
        Pendiente,
        Coincidente,
        NoCoincidente
    };

    private static readonly HashSet<string> EstadosSet = new(Estados, StringComparer.Ordinal);

    public static bool EsEstadoValido(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && EstadosSet.Contains(value.Trim());
    }

    public static string NormalizarEstado(string? value)
    {
        return EsEstadoValido(value) ? value!.Trim() : Pendiente;
    }

    public static ValidationResult? ValidateEstado(object? value, ValidationContext _)
    {
        return EsEstadoValido(value as string)
            ? ValidationResult.Success
            : new ValidationResult("Selecciona un estado de auditoria valido.");
    }

    public static string GetDisplayName(string? value)
    {
        return NormalizarEstado(value) switch
        {
            Pendiente => "Pendiente de auditoria",
            Coincidente => Coincidente,
            NoCoincidente => NoCoincidente,
            _ => "Pendiente de auditoria"
        };
    }

    public static int GetPriority(string? value)
    {
        return NormalizarEstado(value) switch
        {
            Pendiente => 0,
            NoCoincidente => 1,
            Coincidente => 2,
            _ => 0
        };
    }

    public static bool TieneEvidenciaRevisable(string? evidenciaUrl, int totalEvidencias)
    {
        return totalEvidencias > 0 || !string.IsNullOrWhiteSpace(evidenciaUrl);
    }
}
