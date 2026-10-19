using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages;
using BitacoraEvidencias.Web.Services;
using BitacoraEvidencias.Web.Tests.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Pages;

public class AuditoriaModelTests
{
    [Theory]
    [InlineData(typeof(AuditoriaModel))]
    [InlineData(typeof(BitacoraEvidencias.Web.Pages.Auditoria.OficiosPendientesModel))]
    public void OfficeFilter_MvcMetadata_DoesNotRequireAValue(Type pageType)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddDataAnnotations();
        using var provider = services.BuildServiceProvider();
        var metadata = provider.GetRequiredService<IModelMetadataProvider>()
            .GetMetadataForProperty(pageType, nameof(AuditoriaModel.NumeroOficioFiltro));

        // MVC infers Required from non-nullable reference types, even without [Required].
        Assert.False(metadata.IsRequired);
        Assert.False(metadata.IsBindingRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOfficeFilter_LoadsUnfilteredPreviewAndPendingList(string? filter)
    {
        await using var fixture = await Fixture.CreateAsync();
        var page = fixture.Page();
        page.NumeroOficioFiltro = filter;
        await page.OnGetAsync(default);
        Assert.False(page.HasNumeroOficioFiltro);
        Assert.Equal(12, page.TotalOficiosPendientes);
        Assert.Equal(5, page.VisibleOficios.Count);

        var pending = new BitacoraEvidencias.Web.Pages.Auditoria.OficiosPendientesModel(fixture.Context)
        {
            NumeroOficioFiltro = filter
        };
        await pending.OnGetAsync(default);
        Assert.False(pending.HasNumeroOficioFiltro);
        Assert.Equal(12, pending.TotalRows);
    }

    [Fact]
    public async Task Preview_IsBoundedButCountsAllEligibleOffices()
    {
        await using var fixture = await Fixture.CreateAsync();
        var page = fixture.Page();
        await page.OnGetAsync(default);
        Assert.Equal(12, page.TotalOficiosAuditables);
        Assert.Equal(12, page.TotalOficiosPendientes);
        Assert.Equal(5, page.Oficios.Count);
        Assert.Equal(new[] { "OF-012", "OF-011", "OF-010", "OF-009", "OF-008" }, page.VisibleOficios.Select(o => o.NumeroOficio));
        Assert.Equal(page.VisibleOficios[0].Casos[0].Id, page.SelectedCaseId);
    }

    [Fact]
    public async Task SelectionOutsidePreview_RemainsVisibleWithoutLoadingAllOffices()
    {
        await using var fixture = await Fixture.CreateAsync();
        var selected = await fixture.Context.CasosCorreccion.Where(c => c.Oficio!.NumeroOficio == "OF-001").Select(c => c.Id).SingleAsync();
        var page = fixture.Page();
        page.SelectedCaseId = selected;
        await page.OnGetAsync(default);
        Assert.Equal(12, page.TotalOficiosPendientes);
        Assert.Equal(5, page.Oficios.Count);
        Assert.Equal(selected, page.SelectedCase!.Id);
        Assert.Contains(page.VisibleOficios, o => o.NumeroOficio == "OF-001" && o.IsExpanded);
        Assert.DoesNotContain(page.VisibleOficios, o => o.NumeroOficio == "OF-008");
    }

    [Fact]
    public async Task Filter_ResetsSelectionOutsideResultsAndPreservesGlobalCount()
    {
        await using var fixture = await Fixture.CreateAsync();
        var page = fixture.Page();
        page.SelectedCaseId = await fixture.Context.CasosCorreccion.Where(c => c.Oficio!.NumeroOficio == "OF-001").Select(c => c.Id).SingleAsync();
        page.NumeroOficioFiltro = "OF-010";
        await page.OnGetAsync(default);
        Assert.Equal(12, page.TotalOficiosAuditables);
        Assert.Equal(1, page.TotalOficiosPendientes);
        Assert.Equal("OF-010", Assert.Single(page.VisibleOficios).NumeroOficio);
        Assert.Equal("OF-010", page.SelectedCase!.NumeroOficio);
        page.NumeroOficioFiltro = "NO-MATCH";
        await page.OnGetAsync(default);
        Assert.Equal(0, page.TotalOficiosPendientes);
        Assert.Empty(page.VisibleOficios);
        Assert.Null(page.SelectedCaseId);
        Assert.Null(page.SelectedCase);
    }

    private sealed class Fixture(SqliteConnection connection, AppDbContext context) : IAsyncDisposable
    {
        public AppDbContext Context => context;
        public AuditoriaModel Page() => new(context, new CaseAuditService(new RecordingAuditLogService(), new HttpContextAccessor()));

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            for (var number = 1; number <= 14; number++)
            {
                context.Oficios.Add(new Oficio
                {
                    NumeroOficio = $"OF-{number:000}", Asunto = "Test", FechaOficio = new DateTime(2026, 1, number),
                    Casos = [new CasoCorreccion
                    {
                        Consecutivo = 1, NivelEducativo = "Primaria", TipoCorreccion = "Test",
                        EvidenciaUrl = number == 13 ? null : "https://example.invalid/evidence.jpg",
                        AuditoriaEstado = number == 14 ? CatalogosAuditoriaCaso.Coincidente : CatalogosAuditoriaCaso.Pendiente
                    }]
                });
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new Fixture(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
