using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace BitacoraEvidencias.Migration;

public static class ReportMaintenance
{
    public static async Task RunAsync(string mode, string connection)
    {
        await using var db = new SqlServerAppDbContext(new DbContextOptionsBuilder<SqlServerAppDbContext>()
            .UseSqlServer(connection).Options);
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        if (!applied.Any(id => id.EndsWith("_InitialSqlServer")))
            throw new MigrationValidationException("Reportes requiere una base SQL ya migrada; no se crean bases ni se importan datos.");
        var known = db.Database.GetMigrations().ToArray();
        if (applied.Any(id => !known.Contains(id)))
            throw new MigrationValidationException("Hay migraciones desconocidas; se cancela.");
        if (mode == "reports-upgrade")
        {
            if ((await db.Database.GetPendingMigrationsAsync()).Any(id =>
                !id.EndsWith("_PagingIndexes") && !id.EndsWith("_ReportSummaries")))
                throw new MigrationValidationException("La actualizacion contiene migraciones ajenas a reportes.");
            await db.Database.MigrateAsync();
            Console.WriteLine("Esquema de reportes instalado. Los datos originales se conservan. Ejecute reports-rebuild y reports-verify antes de habilitar Reports__SummariesEnabled.");
            return;
        }
        if (!applied.Any(id => id.EndsWith("_ReportSummaries")))
            throw new MigrationValidationException("Primero ejecute reports-upgrade.");
        if (mode == "reports-rebuild")
        {
            await db.Database.ExecuteSqlRawAsync("""
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;
                UPDATE dbo.ReportRefreshState SET Ready=0 WHERE Id=1;
                INSERT dbo.ReportChangeQueue(OficioId)
                    SELECT Id FROM dbo.Oficios UNION SELECT Id FROM dbo.ReportOffices UNION SELECT OficioId FROM dbo.ReportCases;
                COMMIT;
                """);
            // Explicit maintenance only: drain in bounded transactions. The normal
            // worker executes just one batch per minute, never this full loop.
            var batches = 0;
            while (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.ReportChangeQueue").SingleAsync() > 0)
            {
                if (++batches > 10000) throw new MigrationValidationException("Demasiados lotes; revise actividad concurrente y pendientes.");
                await db.Database.ExecuteSqlRawAsync("EXEC dbo.RefreshReportSummaries @BatchSize=25");
            }
            // Also completes initialization for an entirely empty database.
            await db.Database.ExecuteSqlRawAsync("EXEC dbo.RefreshReportSummaries @BatchSize=25");
            Console.WriteLine($"Reconstruccion completada en {batches} lotes. Falta reports-verify.");
            return;
        }

        // Maintenance verification runs without users/worker. A serializable read
        // transaction ensures all comparisons use the same committed source state.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var pending = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.ReportChangeQueue").SingleAsync();
        if (pending != 0) throw new MigrationValidationException("Existen cambios pendientes; complete la actualizacion antes de verificar.");
        var direct = new ReportReadService(db, new ConfigurationBuilder().Build());
        var summary = new ReportReadService(db, new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Reports:SummariesEnabled"] = "true" }).Build());
        await summary.InitializeAsync(default);
        if (!summary.UsesSummary) throw new MigrationValidationException("La carga inicial no ha terminado.");
        // Compare every metric/dimension, including historical dates and offices
        // without cases. Never print row contents or personal data.
        if (JsonSerializer.Serialize(await direct.Cases.OrderBy(c => c.Id).ToListAsync()) !=
            JsonSerializer.Serialize(await summary.Cases.OrderBy(c => c.Id).ToListAsync()) ||
            JsonSerializer.Serialize(await direct.Offices.OrderBy(o => o.Id).ToListAsync()) !=
            JsonSerializer.Serialize(await summary.Offices.OrderBy(o => o.Id).ToListAsync()))
            throw new MigrationValidationException("Los resumenes difieren del origen; no habilite su lectura. Reconstruya e investigue.");
        await transaction.CommitAsync();
        Console.WriteLine("Verificacion de reportes aprobada: todas las dimensiones y metricas coinciden con los datos originales.");
    }
}
