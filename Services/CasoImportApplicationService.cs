using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Admin;
using BitacoraEvidencias.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface ICasoImportApplicationService
{
    Task<CasoImportExecutionResult> ImportAsync(
        ImportCasosModel.CasoImportPreview preview,
        CancellationToken cancellationToken = default);
}

public sealed class CasoImportExecutionResult : ApplicationCommandResult
{
    public int ImportedCount { get; init; }
    public IReadOnlyList<ImportCasosModel.CasoImportRow> FailedRows { get; init; } = [];
}

public sealed class CasoImportApplicationService(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : ICasoImportApplicationService
{
    public async Task<CasoImportExecutionResult> ImportAsync(
        ImportCasosModel.CasoImportPreview preview,
        CancellationToken cancellationToken = default)
    {
        if (!preview.CanImport)
        {
            return new CasoImportExecutionResult
            {
                ErrorMessage = "La vista previa no contiene registros validos para importar."
            };
        }

        var officeTargets = await LoadOfficeTargetsAsync(
            preview.Rows
                .Select(row => row.OficioNumero)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            cancellationToken);

        var importedCount = 0;
        var failedRows = new List<ImportCasosModel.CasoImportRow>();

        foreach (var row in preview.Rows.OrderBy(item => item.SourceRowNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!officeTargets.TryGetValue(row.OficioNumero, out var officeTarget))
            {
                failedRows.Add(row);
                continue;
            }

            if (officeTarget.ExistingLevels.Contains(row.NivelEducativo))
            {
                failedRows.Add(row);
                continue;
            }

            var rowImported = false;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var caso = new CasoCorreccion
                {
                    OficioId = officeTarget.OficioId,
                    Consecutivo = officeTarget.NextConsecutivo,
                    Folio = row.Folio,
                    Cct = row.Cct,
                    Validador = row.Validador,
                    AuditorUsuarioId = row.AuditorUsuarioId,
                    PeriodoInicio = row.PeriodoInicio,
                    PeriodoFin = row.PeriodoFin,
                    Curp = row.Curp,
                    NombreCompleto = row.NombreCompleto,
                    FechaVerificacion = row.FechaVerificacion,
                    NivelEducativo = row.NivelEducativo,
                    TipoCorreccion = row.TipoCorreccion,
                    Observaciones = row.Observaciones,
                    Estatus = row.Estatus,
                    EvidenciaUrl = row.EvidenciaUrl
                };

                dbContext.CasosCorreccion.Add(caso);

                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    officeTarget.NextConsecutivo++;
                    officeTarget.ExistingLevels.Add(row.NivelEducativo);
                    importedCount++;
                    rowImported = true;
                    break;
                }
                catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation("CasosCorreccion.OficioId, CasosCorreccion.NivelEducativo"))
                {
                    break;
                }
                catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation("CasosCorreccion.OficioId, CasosCorreccion.Consecutivo"))
                {
                    officeTarget.NextConsecutivo = await GetNextConsecutivoAsync(officeTarget.OficioId, cancellationToken);
                }
                catch (DbUpdateException)
                {
                    break;
                }
                finally
                {
                    dbContext.ChangeTracker.Clear();
                }
            }

            if (!rowImported)
            {
                failedRows.Add(row);
            }
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Import,
            EntityType = "ImportacionCasos",
            Details = failedRows.Count > 0
                ? $"Importacion masiva parcial de {importedCount} caso(s) desde {preview.FileName}. Filas pendientes: {failedRows.Count}."
                : $"Importacion masiva de {importedCount} caso(s) desde {preview.FileName}.",
            Success = failedRows.Count == 0 || importedCount > 0
        }, cancellationToken);

        return new CasoImportExecutionResult
        {
            ImportedCount = importedCount,
            FailedRows = failedRows
        };
    }

    private async Task<Dictionary<string, OfficeImportTarget>> LoadOfficeTargetsAsync(
        IReadOnlyCollection<string> officeNumbers,
        CancellationToken cancellationToken)
    {
        if (officeNumbers.Count == 0)
        {
            return new Dictionary<string, OfficeImportTarget>(StringComparer.Ordinal);
        }

        var offices = await dbContext.Oficios
            .AsNoTracking()
            .Where(x => officeNumbers.Contains(x.NumeroOficio))
            .Select(x => new { x.Id, x.NumeroOficio })
            .ToListAsync(cancellationToken);

        var officeIds = offices.Select(item => item.Id).ToList();
        var currentCases = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => officeIds.Contains(x.OficioId))
            .Select(x => new OfficeExistingCase
            {
                OficioId = x.OficioId,
                Consecutivo = x.Consecutivo,
                NivelEducativo = x.NivelEducativo
            })
            .ToListAsync(cancellationToken);

        var casesByOffice = currentCases
            .GroupBy(item => item.OficioId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());

        return offices.ToDictionary(
            office => office.NumeroOficio,
            office =>
            {
                casesByOffice.TryGetValue(office.Id, out var officeCases);
                officeCases ??= new List<OfficeExistingCase>();

                return new OfficeImportTarget
                {
                    OficioId = office.Id,
                    ExistingLevels = officeCases
                        .Select(item => item.NivelEducativo)
                        .ToHashSet(StringComparer.Ordinal),
                    NextConsecutivo = officeCases.Count == 0
                        ? 1
                        : officeCases.Max(item => item.Consecutivo) + 1
                };
            },
            StringComparer.Ordinal);
    }

    private async Task<int> GetNextConsecutivoAsync(int oficioId, CancellationToken cancellationToken)
    {
        var maxConsecutivo = await dbContext.CasosCorreccion
            .AsNoTracking()
            .Where(x => x.OficioId == oficioId)
            .MaxAsync(x => (int?)x.Consecutivo, cancellationToken) ?? 0;

        return maxConsecutivo + 1;
    }

    private sealed class OfficeImportTarget
    {
        public int OficioId { get; init; }
        public HashSet<string> ExistingLevels { get; init; } = new(StringComparer.Ordinal);
        public int NextConsecutivo { get; set; }
    }

    private sealed class OfficeExistingCase
    {
        public int OficioId { get; init; }
        public int Consecutivo { get; init; }
        public string NivelEducativo { get; init; } = string.Empty;
    }
}
