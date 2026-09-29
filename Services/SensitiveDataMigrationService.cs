using System.Data.Common;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public sealed class SensitiveDataMigrationService(
    AppDbContext dbContext,
    IPhotoStorageService photoStorageService,
    ISensitiveDataProtectionService sensitiveDataProtection,
    ILogger<SensitiveDataMigrationService> logger)
{
    public async Task<SensitiveDataMigrationResult> ProtectExistingDataAsync(CancellationToken cancellationToken = default)
    {
        if (!sensitiveDataProtection.IsConfigured)
        {
            throw new InvalidOperationException(
                $"Configura la variable de entorno {SensitiveDataProtectionOptions.DefaultEnvironmentVariableName} antes de ejecutar la migracion.");
        }

        await EnsureProtectionSchemaAsync(cancellationToken);

        var result = new SensitiveDataMigrationResult();
        result.StorageFoldersProcessed = await ProtectStorageFolderNamesAsync(result, cancellationToken);
        result.CasesProcessed = await ProtectCasesAsync(cancellationToken);
        result.AuditLogsProcessed = await ProtectAuditLogsAsync(cancellationToken);
        result.EvidenceFilesProcessed = await ProtectEvidenceFilesAsync(result, cancellationToken);

        logger.LogInformation(
            "Migracion de proteccion completada. Carpetas={StorageFolders}, Casos={Cases}, AuditLogs={AuditLogs}, Evidencias={EvidenceFiles}, Errores={Errors}",
            result.StorageFoldersProcessed,
            result.CasesProcessed,
            result.AuditLogsProcessed,
            result.EvidenceFilesProcessed,
            result.Errors.Count);

        return result;
    }

    private async Task<int> ProtectCasesAsync(CancellationToken cancellationToken)
    {
        var cases = await dbContext.CasosCorreccion.ToListAsync(cancellationToken);
        foreach (var caso in cases)
        {
            var entry = dbContext.Entry(caso);
            MarkModified(entry, nameof(CasoCorreccion.Curp));
            MarkModified(entry, nameof(CasoCorreccion.NombreCompleto));
            MarkModified(entry, nameof(CasoCorreccion.Cct));
            MarkModified(entry, nameof(CasoCorreccion.Validador));
            MarkModified(entry, nameof(CasoCorreccion.Folio));
            MarkModified(entry, nameof(CasoCorreccion.EvidenciaUrl));
            MarkModified(entry, nameof(CasoCorreccion.Observaciones));
            MarkModified(entry, nameof(CasoCorreccion.CurpHash));
            MarkModified(entry, nameof(CasoCorreccion.CctHash));
            MarkModified(entry, nameof(CasoCorreccion.FolioHash));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return cases.Count;
    }

    private async Task<int> ProtectStorageFolderNamesAsync(
        SensitiveDataMigrationResult result,
        CancellationToken cancellationToken)
    {
        var oficios = await dbContext.Oficios
            .Include(x => x.Casos)
            .ThenInclude(x => x.Evidencias)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var oficio in oficios)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!photoStorageService.TryProtectExistingOficioFolder(oficio.NumeroOficio, out var errorMessage))
            {
                result.Errors.Add($"Oficio {oficio.NumeroOficio}: {errorMessage}");
                continue;
            }

            foreach (var evidencia in oficio.Casos.SelectMany(x => x.Evidencias))
            {
                var protectedFilePath = photoStorageService.RebaseRelativePathForOficio(
                    evidencia.RutaArchivo,
                    oficio.NumeroOficio,
                    oficio.NumeroOficio);
                var protectedThumbPath = photoStorageService.RebaseRelativePathForOficio(
                    evidencia.RutaMiniatura,
                    oficio.NumeroOficio,
                    oficio.NumeroOficio);

                if (!string.Equals(evidencia.RutaArchivo, protectedFilePath, StringComparison.Ordinal) ||
                    !string.Equals(evidencia.RutaMiniatura, protectedThumbPath, StringComparison.Ordinal))
                {
                    evidencia.RutaArchivo = protectedFilePath;
                    evidencia.RutaMiniatura = protectedThumbPath;
                }
            }

            processed++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return processed;
    }

    private async Task<int> ProtectAuditLogsAsync(CancellationToken cancellationToken)
    {
        var logs = await dbContext.AuditLogs.ToListAsync(cancellationToken);
        foreach (var log in logs)
        {
            var entry = dbContext.Entry(log);
            MarkModified(entry, nameof(AuditLog.Curp));
            MarkModified(entry, nameof(AuditLog.CurpHash));
            MarkModified(entry, nameof(AuditLog.CurpSuffix));
            MarkModified(entry, nameof(AuditLog.Cct));
            MarkModified(entry, nameof(AuditLog.CctHash));
            MarkModified(entry, nameof(AuditLog.CctSuffix));
            MarkModified(entry, nameof(AuditLog.FolioCertificado));
            MarkModified(entry, nameof(AuditLog.FolioCertificadoHash));
            MarkModified(entry, nameof(AuditLog.FolioCertificadoSuffix));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return logs.Count;
    }

    private async Task<int> ProtectEvidenceFilesAsync(
        SensitiveDataMigrationResult result,
        CancellationToken cancellationToken)
    {
        var evidences = await dbContext.EvidenciasFoto
            .AsNoTracking()
            .Select(x => new { x.Id, x.RutaArchivo, x.RutaMiniatura })
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var evidencia in evidences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!photoStorageService.TryProtectExistingEvidenceFile(evidencia.RutaArchivo, out var fileError))
            {
                result.Errors.Add($"Evidencia #{evidencia.Id}, archivo: {fileError}");
                continue;
            }

            if (!photoStorageService.TryProtectExistingEvidenceFile(evidencia.RutaMiniatura, out var thumbError))
            {
                result.Errors.Add($"Evidencia #{evidencia.Id}, miniatura: {thumbError}");
                continue;
            }

            processed++;
        }

        return processed;
    }

    private async Task EnsureProtectionSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await EnsureColumnAsync(connection, "CasosCorreccion", "CurpHash", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "CasosCorreccion", "CctHash", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "CasosCorreccion", "FolioHash", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "AuditLogs", "CurpHash", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "AuditLogs", "CurpSuffix", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "AuditLogs", "CctHash", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "AuditLogs", "CctSuffix", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "AuditLogs", "FolioCertificadoHash", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "AuditLogs", "FolioCertificadoSuffix", "TEXT NULL", cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        DbConnection connection,
        string tableName,
        string columnName,
        string definition,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, tableName, columnName, cancellationToken))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {definition};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void MarkModified<TEntity>(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> entry,
        string propertyName)
        where TEntity : class
    {
        entry.Property(propertyName).IsModified = true;
    }
}

public sealed class SensitiveDataMigrationResult
{
    public int CasesProcessed { get; set; }
    public int StorageFoldersProcessed { get; set; }
    public int AuditLogsProcessed { get; set; }
    public int EvidenceFilesProcessed { get; set; }
    public List<string> Errors { get; } = [];
}
