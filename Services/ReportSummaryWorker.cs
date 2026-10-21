using BitacoraEvidencias.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BitacoraEvidencias.Web.Services;

public sealed class ReportSummaryWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<ReportSummaryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Reports:SummariesEnabled")) return;
        // One bounded batch per tick, including startup. The database lock also protects
        // against overlapping application instances. Failed transactions retain the queue.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.SetCommandTimeout(10);
                await db.Database.ExecuteSqlRawAsync("EXEC dbo.RefreshReportSummaries @BatchSize=25", stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                // SQL exception text can contain captured values; keep logs free of them.
                logger.LogWarning("Resumenes de reportes: lote pendiente de reintento ({ErrorType}).", exception.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
