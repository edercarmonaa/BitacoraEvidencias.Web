using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public static class IncorrectAuditOfficeQueries
{
    public static IOrderedQueryable<IncorrectAuditOfficeRow> Offices(AppDbContext context) => context.Oficios
        .AsNoTracking()
        .Where(o => o.Casos.Any() &&
            o.Casos.All(c => c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl.Length > 0)) &&
            o.Casos.All(c => c.AuditoriaEstado != null &&
                (c.AuditoriaEstado.Trim() == CatalogosAuditoriaCaso.Coincidente || c.AuditoriaEstado.Trim() == CatalogosAuditoriaCaso.NoCoincidente)) &&
            o.Casos.Any(c => c.AuditoriaEstado.Trim() == CatalogosAuditoriaCaso.NoCoincidente))
        .OrderByDescending(o => o.FechaOficio).ThenBy(o => o.NumeroOficio).ThenBy(o => o.Id)
        .Select(o => new IncorrectAuditOfficeRow
        {
            Id = o.Id, NumeroOficio = o.NumeroOficio, FechaOficio = o.FechaOficio,
            Asunto = o.Asunto, TotalCasos = o.Casos.Count
        })
        .OrderByDescending(o => o.FechaOficio).ThenBy(o => o.NumeroOficio).ThenBy(o => o.Id);

    public static IOrderedQueryable<IncorrectAuditCaseRow> Cases(AppDbContext context) => context.CasosCorreccion
        .AsNoTracking()
        .Where(c => c.AuditoriaEstado == CatalogosAuditoriaCaso.NoCoincidente &&
            (c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl.Length > 0)))
        .Select(c => new IncorrectAuditCaseRow
        {
            CasoId = c.Id, OficioId = c.OficioId, NumeroOficio = c.Oficio!.NumeroOficio,
            FechaOficio = c.Oficio.FechaOficio, Consecutivo = c.Consecutivo, Curp = c.Curp,
            NivelEducativo = c.NivelEducativo, TipoCorreccion = c.TipoCorreccion
        })
        .OrderByDescending(c => c.FechaOficio).ThenBy(c => c.NumeroOficio).ThenBy(c => c.Consecutivo).ThenBy(c => c.CasoId);

    public static Task<List<IncorrectAuditOfficeRow>> LoadAsync(AppDbContext context, CancellationToken cancellationToken = default)
        => Offices(context).ToListAsync(cancellationToken);

    public static Task<List<IncorrectAuditCaseRow>> LoadCasesAsync(AppDbContext context, CancellationToken cancellationToken = default)
        => Cases(context).ToListAsync(cancellationToken);
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
