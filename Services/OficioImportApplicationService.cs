using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Pages.Admin;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public interface IOficioImportApplicationService
{
    Task<OficioImportExecutionResult> ImportAsync(
        ImportOficiosModel.OficioImportPreview preview,
        CancellationToken cancellationToken = default);
}

public sealed class OficioImportExecutionResult : ApplicationCommandResult
{
    public int ImportedCount { get; init; }
    public IReadOnlyList<ImportOficiosModel.OficioImportRow> FailedRows { get; init; } = [];
}

public sealed class OficioImportApplicationService(
    AppDbContext dbContext,
    IAuditLogService auditLogService) : IOficioImportApplicationService
{
    public async Task<OficioImportExecutionResult> ImportAsync(
        ImportOficiosModel.OficioImportPreview preview,
        CancellationToken cancellationToken = default)
    {
        if (!preview.CanImport)
        {
            return new OficioImportExecutionResult
            {
                ErrorMessage = "La vista previa no contiene registros validos para importar."
            };
        }

        var existingNumbers = await dbContext.Oficios
            .AsNoTracking()
            .Select(x => x.NumeroOficio)
            .ToListAsync(cancellationToken);

        var importedCount = 0;
        var failedRows = new List<ImportOficiosModel.OficioImportRow>();

        foreach (var row in preview.Rows.OrderBy(item => item.SourceRowNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (existingNumbers.Contains(row.NumeroOficio, StringComparer.Ordinal))
            {
                failedRows.Add(row);
                continue;
            }

            if (OficioStoragePath.TryFindFolderCollision(row.NumeroOficio, existingNumbers, out _))
            {
                failedRows.Add(row);
                continue;
            }

            dbContext.Oficios.Add(new Oficio
            {
                NumeroOficio = row.NumeroOficio,
                FechaOficio = row.FechaOficio,
                Asunto = row.Asunto,
                Notas = row.Notas
            });

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                existingNumbers.Add(row.NumeroOficio);
                importedCount++;
            }
            catch (DbUpdateException)
            {
                failedRows.Add(row);
            }
            finally
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        await auditLogService.WriteAsync(new AuditLogEntry
        {
            EventType = AuditEvents.Import,
            EntityType = "ImportacionOficios",
            Details = failedRows.Count > 0
                ? $"Importacion masiva parcial de {importedCount} oficio(s) desde {preview.FileName}. Filas pendientes: {failedRows.Count}."
                : $"Importacion masiva de {importedCount} oficio(s) desde {preview.FileName}.",
            Success = failedRows.Count == 0 || importedCount > 0
        }, cancellationToken);

        return new OficioImportExecutionResult
        {
            ImportedCount = importedCount,
            FailedRows = failedRows
        };
    }
}
