using System.Data.Common;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.ViewComponents;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.ViewComponents;

public class DashboardOverviewTests
{
    [Fact]
    public async Task InvokeAsync_WithAsynchronousDatabase_ReturnsDashboardWithoutOverlappingQueries()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).AddInterceptors(new DelayedReaderInterceptor()).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Oficios.Add(new Oficio { NumeroOficio = "TEST-001", Asunto = "Test" });
        await context.SaveChangesAsync();
        context.CasosCorreccion.Add(new CasoCorreccion
        {
            OficioId = context.Oficios.Single().Id, Consecutivo = 1,
            NivelEducativo = "Primaria", TipoCorreccion = "Test"
        });
        await context.SaveChangesAsync();
        var component = new DashboardOverviewViewComponent(context)
        {
            ViewComponentContext = new ViewComponentContext
            {
                ViewContext = new ViewContext
                {
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                }
            }
        };

        var result = Assert.IsType<ViewViewComponentResult>(await component.InvokeAsync());
        var model = Assert.IsType<DashboardOverviewViewModel>(Assert.IsType<ViewDataDictionary<DashboardOverviewViewModel>>(result.ViewData).Model);
        Assert.Equal(new[] { 1, 1, 0 }, model.StatCards.Select(card => card.Value));
        Assert.Equal("TEST-001", Assert.Single(model.OficiosRecientes).NumeroOficio);
        Assert.Equal("TEST-001", Assert.Single(model.CasosPendientes).NumeroOficio);
    }

    private sealed class DelayedReaderInterceptor : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            // SQLite often completes synchronously. Force the real asynchronous overlap
            // that SQL Server exposes, without needing production credentials.
            await Task.Delay(25, cancellationToken);
            return result;
        }
    }
}
