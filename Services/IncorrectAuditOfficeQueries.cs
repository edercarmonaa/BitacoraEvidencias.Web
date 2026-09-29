using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public static class IncorrectAuditOfficeQueries
{
    public static async Task<List<IncorrectAuditOfficeRow>> LoadAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var auditableOffices = await dbContext.Oficios
            .AsNoTracking()
            .Where(x =>
                x.Casos.Any() &&
                x.Casos.All(c => c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl != string.Empty)))
            .Select(x => new IncorrectAuditOfficeCandidate
            {
                Id = x.Id,
                NumeroOficio = x.NumeroOficio,
                FechaOficio = x.FechaOficio,
                Asunto = x.Asunto,
                TotalCasos = x.Casos.Count,
                Estados = x.Casos
                    .Select(c => c.AuditoriaEstado)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return auditableOffices
            .Where(x => IsIncorrectAuditOffice(x.Estados))
            .OrderByDescending(x => x.FechaOficio)
            .ThenBy(x => x.NumeroOficio, StringComparer.Ordinal)
            .Select(x => new IncorrectAuditOfficeRow
            {
                Id = x.Id,
                NumeroOficio = x.NumeroOficio,
                FechaOficio = x.FechaOficio,
                Asunto = x.Asunto,
                TotalCasos = x.TotalCasos
            })
            .ToList();
    }

    public static async Task<List<IncorrectAuditCaseRow>> LoadCasesAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var cases = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x =>
                x.AuditoriaEstado == CatalogosAuditoriaCaso.NoCoincidente &&
                (x.Evidencias.Any() || (x.EvidenciaUrl != null && x.EvidenciaUrl != string.Empty)))
            .Select(x => new IncorrectAuditCaseRow
            {
                CasoId = x.Id,
                OficioId = x.OficioId,
                NumeroOficio = x.Oficio!.NumeroOficio,
                FechaOficio = x.Oficio.FechaOficio,
                Consecutivo = x.Consecutivo,
                Curp = x.Curp,
                NivelEducativo = x.NivelEducativo,
                TipoCorreccion = x.TipoCorreccion
            })
            .ToListAsync(cancellationToken);

        return cases
            .OrderByDescending(x => x.FechaOficio)
            .ThenBy(x => x.NumeroOficio, StringComparer.Ordinal)
            .ThenBy(x => x.Consecutivo)
            .ToList();
    }

    private static bool IsIncorrectAuditOffice(IReadOnlyCollection<string> estados)
    {
        if (estados.Count == 0)
        {
            return false;
        }

        return estados.All(estado => CatalogosAuditoriaCaso.NormalizarEstado(estado) != CatalogosAuditoriaCaso.Pendiente) &&
               estados.Any(estado => CatalogosAuditoriaCaso.NormalizarEstado(estado) == CatalogosAuditoriaCaso.NoCoincidente);
    }

    private sealed class IncorrectAuditOfficeCandidate
    {
        public int Id { get; init; }
        public string NumeroOficio { get; init; } = string.Empty;
        public DateTime FechaOficio { get; init; }
        public string Asunto { get; init; } = string.Empty;
        public int TotalCasos { get; init; }
        public List<string> Estados { get; init; } = [];
    }
}

public sealed class IncorrectAuditOfficeRow
{
    public int Id { get; init; }
    public string NumeroOficio { get; init; } = string.Empty;
    public DateTime FechaOficio { get; init; }
    public string Asunto { get; init; } = string.Empty;
    public int TotalCasos { get; init; }
}

public sealed class IncorrectAuditCaseRow
{
    public int CasoId { get; init; }
    public int OficioId { get; init; }
    public string NumeroOficio { get; init; } = string.Empty;
    public DateTime FechaOficio { get; init; }
    public int Consecutivo { get; init; }
    public string? Curp { get; init; }
    public string NivelEducativo { get; init; } = string.Empty;
    public string TipoCorreccion { get; init; } = string.Empty;
    public string ConsecutivoTexto => Consecutivo.ToString("000");
}
