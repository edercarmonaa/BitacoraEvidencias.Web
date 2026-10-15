using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BitacoraEvidencias.Web.Services;

public sealed class PageQueryTiming
{
    public int Commands { get; set; }
    public double CommandMilliseconds { get; set; }
    public double ConnectionMilliseconds { get; set; }
}

public sealed class PageQueryTimingFilter(PageQueryTiming timing, ILogger<PageQueryTimingFilter> logger) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var watch = Stopwatch.StartNew();
        try { await next(); }
        finally
        {
            // Log only the page template and timings, never route/query values or SQL.
            logger.LogInformation(
                "Pagina {Page}: handler {ElapsedMs} ms; comandos {Commands}, SQL {SqlMs:F0} ms, conexion {ConnectionMs:F0} ms; cancelada {Aborted}.",
                context.ActionDescriptor.ViewEnginePath, watch.ElapsedMilliseconds,
                timing.Commands, timing.CommandMilliseconds, timing.ConnectionMilliseconds,
                context.HttpContext.RequestAborted.IsCancellationRequested);
        }
    }
}

public sealed class QueryTimingInterceptor(PageQueryTiming timing) : DbCommandInterceptor
{
    private void Record(CommandExecutedEventData data)
    {
        timing.Commands++;
        timing.CommandMilliseconds += data.Duration.TotalMilliseconds;
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    { Record(eventData); return result; }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    { Record(eventData); return ValueTask.FromResult(result); }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    { Record(eventData); return result; }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    { Record(eventData); return ValueTask.FromResult(result); }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    { Record(eventData); return result; }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    { Record(eventData); return ValueTask.FromResult(result); }
}

public sealed class ConnectionTimingInterceptor(PageQueryTiming timing) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => timing.ConnectionMilliseconds += eventData.Duration.TotalMilliseconds;

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        timing.ConnectionMilliseconds += eventData.Duration.TotalMilliseconds;
        return Task.CompletedTask;
    }
}
