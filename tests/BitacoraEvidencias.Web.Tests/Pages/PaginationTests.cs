using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages;
using BitacoraEvidencias.Web.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Pages;

public class PaginationTests
{
    [Theory]
    [InlineData("oficio", "asc", 8, 1)]
    [InlineData("oficio", "desc", 38, -1)]
    [InlineData("fecha", "desc", 8, 1)]
    public async Task Search_TwoStagePagePreservesRequestedOrder(string sort, string direction, int start, int step)
    {
        await using var fixture = await Fixture.CreateAsync();
        var protection = new SensitiveDataProtectionService(Options.Create(new SensitiveDataProtectionOptions
        { KeyEnvironmentVariable = "UNCONFIGURED_PAGINATION_TEST_KEY" }));
        var page = new BusquedaModel(fixture.Context, protection)
        { SortBy = sort, SortDirection = direction, PageSize = 7, PageNumber = 2 };
        await page.OnGetAsync();
        Assert.Equal(Enumerable.Range(0, 7).Select(i => $"OF-{start + i * step:000}"),
            page.Resultados.Select(r => r.NumeroOficio));
        var empty = new BusquedaModel(fixture.Context, protection) { NumeroOficio = "NONEXISTENT" };
        await empty.OnGetAsync();
        Assert.Empty(empty.Resultados);
        Assert.Equal(0, empty.TotalRows);
    }

    [Fact]
    public async Task ProtectedSearch_UsesExactIndexAndDecryptsOnlyVisibleRows()
    {
        var protection = new CountingProtection();
        await using var fixture = await Fixture.CreateAsync(protection);
        protection.Reads = 0;
        var page = new BusquedaModel(fixture.Context, protection) { PageSize = 7, PageNumber = 2, SortBy = "nombre" };
        await page.OnGetAsync();
        Assert.Equal(45, page.TotalRows);
        Assert.Equal(7, page.Resultados.Count);
        Assert.Equal(14, protection.Reads);
        Assert.Equal("fecha", page.ActiveSortBy);
        var exact = new BusquedaModel(fixture.Context, protection) { Curp = "TEST001", UseContains = true };
        await exact.OnGetAsync();
        Assert.Equal(1, exact.TotalRows);
        Assert.Equal("TEST001", Assert.Single(exact.Resultados).Curp);
    }

    [Fact]
    public async Task Search_PreservesFiltersAndStableNonOverlappingPages()
    {
        await using var fixture = await Fixture.CreateAsync();
        var protection = new SensitiveDataProtectionService(Options.Create(new SensitiveDataProtectionOptions
        { KeyEnvironmentVariable = "UNCONFIGURED_PAGINATION_TEST_KEY" }));
        var first = new BusquedaModel(fixture.Context, protection) { PageSize = 7 };
        var second = new BusquedaModel(fixture.Context, protection) { PageSize = 7, PageNumber = 2 };
        await first.OnGetAsync();
        await second.OnGetAsync();
        Assert.Equal(45, first.TotalRows);
        Assert.Equal(7, first.Resultados.Count);
        Assert.Equal(7, second.Resultados.Count);
        Assert.Empty(first.Resultados.Select(r => r.CasoId).Intersect(second.Resultados.Select(r => r.CasoId)));
        var repeated = new BusquedaModel(fixture.Context, protection) { PageSize = 7, PageNumber = 2 };
        await repeated.OnGetAsync();
        Assert.Equal(second.Resultados.Select(r => r.CasoId), repeated.Resultados.Select(r => r.CasoId));
        var filtered = new BusquedaModel(fixture.Context, protection) { NumeroOficio = "OF-00", PageSize = 3, PageNumber = 99 };
        await filtered.OnGetAsync();
        Assert.Equal(9, filtered.TotalRows);
        Assert.Equal(3, filtered.PageNumber);
        Assert.Equal(3, filtered.Resultados.Count);
    }

    [Fact]
    public async Task ListsAndReports_PageInDatabaseWithCorrectTotals()
    {
        await using var fixture = await Fixture.CreateAsync();
        var offices = new BitacoraEvidencias.Web.Pages.Oficios.IndexModel(fixture.Context) { PageNumber = 2, PageSize = 7 };
        await offices.OnGetAsync();
        Assert.Equal(45, offices.TotalRows);
        Assert.Equal(7, offices.Oficios.Count);
        var pending = new BitacoraEvidencias.Web.Pages.Reportes.PendientesEvidenciaModel(fixture.Context) { PageNumber = 2, PageSize = 7 };
        await pending.OnGetAsync();
        Assert.Equal(15, pending.TotalRows);
        Assert.Equal(7, pending.Items.Count);
        var incomplete = new BitacoraEvidencias.Web.Pages.Reportes.CasosNoCompletadosModel(fixture.Context) { PageNumber = 2, PageSize = 7 };
        await incomplete.OnGetAsync();
        Assert.Equal(45, incomplete.TotalRows);
        Assert.Equal(7, incomplete.Items.Count);
        var incorrect = new BitacoraEvidencias.Web.Pages.Reportes.OficiosConEvidenciaIncorrectaModel(fixture.Context) { PageNumber = 2, PageSize = 7 };
        await incorrect.OnGetAsync(default);
        Assert.Equal(30, incorrect.TotalRows);
        Assert.Equal(7, incorrect.Items.Count);
        var users = new BitacoraEvidencias.Web.Pages.Admin.UsersModel(fixture.Context, null!) { PageNumber = 2, PageSize = 7 };
        await users.OnGetAsync();
        Assert.Equal(45, users.TotalRows);
        Assert.Equal(7, users.Users.Count);
        var logs = new BitacoraEvidencias.Web.Pages.Admin.AuditLogModel(fixture.Context) { PageNumber = 2, PageSize = 7 };
        await logs.OnGetAsync();
        Assert.Equal(45, logs.TotalRows);
        Assert.Equal(7, logs.Items.Count);
        var report = new ReportesModel(fixture.Context);
        await report.OnGetAsync();
        Assert.Equal(30, report.TotalCasosConEvidenciaIncorrecta);
        Assert.Equal(30, report.TotalOficiosConEvidenciaIncorrecta);
        Assert.Equal(5, report.CasosConEvidenciaIncorrecta.Count);
        Assert.Equal(5, report.OficiosConEvidenciaIncorrecta.Count);
    }

    [Fact]
    public async Task EvidenceGallery_IsBoundedAndClampsInvalidPage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var caseId = await fixture.Context.CasosCorreccion.Where(c => c.Oficio!.NumeroOficio == "OF-001").Select(c => c.Id).SingleAsync();
        fixture.Context.EvidenciasFoto.AddRange(Enumerable.Range(1, 35).Select(_ => new EvidenciaFoto
        { CasoCorreccionId = caseId, RutaArchivo = "test.jpg", RutaMiniatura = "thumb.jpg", TipoEvidencia = "Extra" }));
        await fixture.Context.SaveChangesAsync();
        var page = new BitacoraEvidencias.Web.Pages.Casos.DetailsModel(fixture.Context, null!, null!, null!)
        { PageSize = 20, PageNumber = int.MaxValue };
        await page.OnGetAsync(caseId);
        Assert.Equal(35, page.TotalRows);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(15, page.Evidencias.Count);
    }

    [Fact]
    public async Task Search_StopsWhenRequestIsCancelled()
    {
        await using var fixture = await Fixture.CreateAsync();
        var protection = new SensitiveDataProtectionService(Options.Create(new SensitiveDataProtectionOptions
        { KeyEnvironmentVariable = "UNCONFIGURED_PAGINATION_TEST_KEY" }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BusquedaModel(fixture.Context, protection).OnGetAsync(cancellation.Token));
    }

    private sealed class Fixture(SqliteConnection connection, AppDbContext context) : IAsyncDisposable
    {
        public AppDbContext Context => context;
        public static async Task<Fixture> CreateAsync(ISensitiveDataProtectionService? protection = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection);
            if (protection is not null) builder.EnableServiceProviderCaching(false);
            var context = protection is null ? new AppDbContext(builder.Options) : new AppDbContext(builder.Options, protection);
            await context.Database.EnsureCreatedAsync();
            for (var number = 1; number <= 45; number++)
            {
                context.Oficios.Add(new Oficio
                {
                    NumeroOficio = $"OF-{number:000}", Asunto = "Test", FechaOficio = new DateTime(2026, 1, 1),
                    Casos = [new CasoCorreccion
                    {
                        Consecutivo = 1, NivelEducativo = "Primaria", TipoCorreccion = "Test", CreadoEn = new DateTime(2026, 1, 1),
                        Curp = $"TEST{number:000}", NombreCompleto = $"Test name {number}",
                        EvidenciaUrl = number % 3 == 0 ? null : "https://example.invalid/evidence.jpg",
                        AuditoriaEstado = CatalogosAuditoriaCaso.NoCoincidente
                    }]
                });
                context.UsuariosSistema.Add(new UsuarioSistema
                { Usuario = $"user{number:000}", UsuarioNormalizado = $"USER{number:000}", PasswordHash = "test", PasswordSalt = "test", Rol = "Capturista" });
                context.AuditLogs.Add(new AuditLog { EventType = "TEST", OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-1) });
            }
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new Fixture(connection, context);
        }
        public async ValueTask DisposeAsync() { await context.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class CountingProtection : ISensitiveDataProtectionService
    {
        public int Reads { get; set; }
        public bool IsConfigured => true;
        public bool IsProtected(string? value) => value?.StartsWith("wrapped:") == true;
        public string? ProtectString(string? value) => string.IsNullOrEmpty(value) ? null : "wrapped:" + value;
        public string? UnprotectString(string? value) { Reads++; return value?.Replace("wrapped:", ""); }
        public string? BlindIndex(string? value) => value is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value.ToUpperInvariant())));
        public string? Suffix(string? value, int length = 4) => value;
        public byte[] ProtectBytes(byte[] plaintext) => throw new NotSupportedException();
        public byte[] UnprotectBytes(byte[] protectedBytes) => throw new NotSupportedException();
        public bool IsProtectedPayload(ReadOnlySpan<byte> payload) => false;
    }
}
