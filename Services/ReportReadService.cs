using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

// These read models contain no decrypted personal data or storage paths.
public sealed class ReportCase
{
    public int Id { get; set; }
    public int OficioId { get; set; }
    public string NumeroOficio { get; set; } = "";
    public int Consecutivo { get; set; }
    public DateTime CreadoEn { get; set; }
    public DateTime? FechaVerificacion { get; set; }
    public string NivelEducativo { get; set; } = "";
    public string TipoCorreccion { get; set; } = "";
    public string Estatus { get; set; } = "";
    public int Fotos { get; set; }
    public bool TieneUrl { get; set; }
    public bool Incorrecto { get; set; }
}

public sealed class ReportOffice
{
    public int Id { get; set; }
    public DateTime FechaOficio { get; set; }
    public DateTime? PrimeraFechaVerificacion { get; set; }
    public bool Correcto { get; set; }
    public bool Incorrecto { get; set; }
}

public sealed class ReportRefreshState
{
    public bool Ready { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class ReportReadService(AppDbContext db, IConfiguration configuration)
{
    public DateTime? UpdatedAtUtc { get; private set; }
    public bool UsesSummary { get; private set; }

    public async Task InitializeAsync(CancellationToken token)
    {
        if (!db.Database.IsSqlServer() || !configuration.GetValue<bool>("Reports:SummariesEnabled")) return;
        var state = await db.Database.SqlQueryRaw<ReportRefreshState>(
            "SELECT Ready, UpdatedAtUtc FROM dbo.ReportRefreshState WHERE Id=1").SingleAsync(token);
        UsesSummary = state.Ready;
        UpdatedAtUtc = UsesSummary ? state.UpdatedAtUtc : null;
    }

    public IQueryable<ReportCase> Cases => UsesSummary
        ? db.Database.SqlQueryRaw<ReportCase>("SELECT * FROM dbo.ReportCases")
        : db.CasosCorreccion.AsNoTracking().Select(c => new ReportCase
        {
            Id = c.Id, OficioId = c.OficioId, NumeroOficio = c.Oficio!.NumeroOficio,
            Consecutivo = c.Consecutivo, CreadoEn = c.CreadoEn, FechaVerificacion = c.FechaVerificacion,
            NivelEducativo = c.NivelEducativo, TipoCorreccion = c.TipoCorreccion, Estatus = c.Estatus,
            Fotos = c.Evidencias.Count, TieneUrl = c.EvidenciaUrl != null && c.EvidenciaUrl.Length > 0,
            Incorrecto = c.AuditoriaEstado == CatalogosAuditoriaCaso.NoCoincidente &&
                (c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl.Length > 0))
        });

    public IQueryable<ReportOffice> Offices => UsesSummary
        ? db.Database.SqlQueryRaw<ReportOffice>("SELECT * FROM dbo.ReportOffices")
        : db.Oficios.AsNoTracking().Select(o => new ReportOffice
        {
            Id = o.Id, FechaOficio = o.FechaOficio,
            PrimeraFechaVerificacion = o.Casos.Where(c => c.FechaVerificacion.HasValue).Min(c => c.FechaVerificacion),
            Correcto = o.Casos.Any() &&
                o.Casos.All(c => c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl.Length > 0)) &&
                o.Casos.All(c => c.AuditoriaEstado != null && c.AuditoriaEstado.Trim() == CatalogosAuditoriaCaso.Coincidente),
            Incorrecto = o.Casos.Any() &&
                o.Casos.All(c => c.Evidencias.Any() || (c.EvidenciaUrl != null && c.EvidenciaUrl.Length > 0)) &&
                o.Casos.All(c => c.AuditoriaEstado != null &&
                    (c.AuditoriaEstado.Trim() == CatalogosAuditoriaCaso.Coincidente || c.AuditoriaEstado.Trim() == CatalogosAuditoriaCaso.NoCoincidente)) &&
                o.Casos.Any(c => c.AuditoriaEstado.Trim() == CatalogosAuditoriaCaso.NoCoincidente)
        });
}
