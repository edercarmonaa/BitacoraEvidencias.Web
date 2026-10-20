using System.Diagnostics;
using System.Text.Json;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages;
using BitacoraEvidencias.Web.Pages.Reportes;
using BitacoraEvidencias.Web.Services;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Migration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace BitacoraEvidencias.Web.Tests.Data;

public sealed class LocalReportSqlFactAttribute : FactAttribute
{
    public LocalReportSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BITACORA_REPORT_TEST_SQL")))
            Skip = "Requires an explicit local SQL master connection; creates its own disposable database.";
    }
}

public sealed class ReportSummaryIntegrationTests(ITestOutputHelper output)
{
    [LocalReportSqlFact]
    public async Task Summaries_PreserveMetrics_HandleEditsDeletesRetriesAndRollback()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("BITACORA_REPORT_TEST_SQL"));
        Assert.Equal("master", connection.InitialCatalog, ignoreCase: true);
        Assert.True(connection.DataSource is @".\Auditoria" or "localhost\\Auditoria");
        var database = "Bitacora_ReportTest_" + Guid.NewGuid().ToString("N");
        await using var admin = new SqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new SqlCommand($"CREATE DATABASE [{database}]", admin))
            await create.ExecuteNonQueryAsync();
        connection.InitialCatalog = database;
        connection.Pooling = false;
        try
        {
            var keyVariable = "BITACORA_SYNTHETIC_REPORT_KEY_" + Guid.NewGuid().ToString("N");
            var protection = new SensitiveDataProtectionService(Options.Create(new SensitiveDataProtectionOptions
            {
                KeyEnvironmentVariable = keyVariable
            }));
            Environment.SetEnvironmentVariable(keyVariable, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            try { Assert.True(protection.IsConfigured); }
            finally { Environment.SetEnvironmentVariable(keyVariable, null); }
            await using var db = new SqlServerAppDbContext(new DbContextOptionsBuilder<SqlServerAppDbContext>()
                .UseSqlServer(connection.ConnectionString).EnableServiceProviderCaching(false).Options, protection);
            await db.Database.MigrateAsync();
            var emptyOffice = new Oficio { NumeroOficio = "EMPTY", Asunto = "Synthetic", FechaOficio = new(2026, 1, 1) };
            var office = new Oficio { NumeroOficio = "TEST-1", Asunto = "Synthetic", FechaOficio = new(2025, 1, 1) };
            db.Oficios.AddRange(emptyOffice, office);
            var first = new CasoCorreccion
            {
                Oficio = office, Consecutivo = 1, NivelEducativo = CatalogosCaso.NivelesEducativos[0],
                TipoCorreccion = "CURP", Estatus = "Completado", AuditoriaEstado = "Coincidente",
                FechaVerificacion = new(2025, 12, 20), EvidenciaUrl = "https://example.test/synthetic-link"
            };
            var second = new CasoCorreccion
            {
                Oficio = office, Consecutivo = 2, NivelEducativo = CatalogosCaso.NivelesEducativos[1],
                TipoCorreccion = "CURP", Estatus = "Pendiente", AuditoriaEstado = "No coincidente",
                FechaVerificacion = new(2026, 1, 5)
            };
            db.CasosCorreccion.AddRange(first, second);
            await db.SaveChangesAsync(); // Also validates EF inserts with enabled triggers.
            var photo = new EvidenciaFoto
            {
                CasoCorreccionId = second.Id, RutaArchivo = "synthetic.jpg", RutaMiniatura = "synthetic-thumb.jpg",
                TipoEvidencia = CatalogosCaso.TiposEvidencia[0], FechaEvidencia = new(2026, 1, 5)
            };
            db.EvidenciasFoto.Add(photo);
            await db.SaveChangesAsync();

            // Partial initialization must never expose an incomplete summary.
            await db.Database.ExecuteSqlRawAsync("EXEC dbo.RefreshReportSummaries @BatchSize=1");
            var initializing = await Reader(db, true);
            Assert.False(initializing.UsesSummary);
            await Drain(db);
            await AssertEquivalent(db);
            var before = await db.Database.SqlQueryRaw<DateTime>("SELECT UpdatedAtUtc AS Value FROM dbo.ReportRefreshState").SingleAsync();
            await Drain(db);
            Assert.Equal(before, await db.Database.SqlQueryRaw<DateTime>("SELECT UpdatedAtUtc AS Value FROM dbo.ReportRefreshState").SingleAsync());

            // Historical edits affect old buckets; removing evidence invalidates audits.
            first.FechaVerificacion = new(2026, 2, 1);
            first.EvidenciaUrl = null;
            second.TipoCorreccion = "NOMBRE";
            office.NumeroOficio = "TEST-RENAMED";
            db.EvidenciasFoto.Remove(photo);
            await db.SaveChangesAsync();
            await Drain(db);
            await AssertEquivalent(db);

            // Source writes and notifications both roll back together.
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                office.Asunto = "Rollback";
                await db.SaveChangesAsync();
                Assert.True(await Pending(db) > 0);
                await transaction.RollbackAsync();
            }
            Assert.Equal(0, await Pending(db));

            // Simulate an event arriving after the worker's watermark was captured.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER dbo.Test_ReportLateEvent ON dbo.ReportCases AFTER INSERT AS
                BEGIN SET NOCOUNT ON; INSERT dbo.ReportChangeQueue(OficioId) SELECT DISTINCT OficioId FROM inserted; END
                """);
            second.Estatus = "Completado";
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("EXEC dbo.RefreshReportSummaries @BatchSize=25");
            Assert.True(await Pending(db) > 0);
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.Test_ReportLateEvent");

            // Failure leaves both previous summaries and all pending work intact.
            var previous = JsonSerializer.Serialize(await (await Reader(db, true)).Cases.OrderBy(c => c.Id).ToListAsync());
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER dbo.Test_ReportFailure ON dbo.ReportCases AFTER INSERT AS
                BEGIN THROW 50555,'Synthetic summary failure',1; END
                """);
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("EXEC dbo.RefreshReportSummaries @BatchSize=25"));
            Assert.True(await Pending(db) > 0);
            Assert.Equal(previous, JsonSerializer.Serialize(await (await Reader(db, true)).Cases.OrderBy(c => c.Id).ToListAsync()));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.Test_ReportFailure");
            await Drain(db);
            await AssertEquivalent(db);

            // Cascade deletion leaves no orphaned counts or read model rows.
            db.Oficios.Remove(office);
            await db.SaveChangesAsync();
            await Drain(db);
            await AssertEquivalent(db);
            Assert.Empty(await (await Reader(db, true)).Cases.ToListAsync());
            // Exercise the exact maintenance commands as well as the procedure.
            await ReportMaintenance.RunAsync("reports-upgrade", connection.ConnectionString);
            await ReportMaintenance.RunAsync("reports-rebuild", connection.ConnectionString);
            await ReportMaintenance.RunAsync("reports-verify", connection.ConnectionString);

            // Representative synthetic volume, no production data or secrets.
            for (var i = 0; i < 824; i++)
            {
                var item = new Oficio { NumeroOficio = $"PERF-{i:0000}", Asunto = "Synthetic", FechaOficio = new(2026, 1, 1) };
                item.Casos.Add(new CasoCorreccion
                {
                    Consecutivo = 1, NivelEducativo = CatalogosCaso.NivelesEducativos[0], TipoCorreccion = "CURP",
                    Estatus = "Completado", AuditoriaEstado = "Coincidente", FechaVerificacion = new(2026, 1, 15),
                    Evidencias = [new EvidenciaFoto { RutaArchivo = "fake.jpg", RutaMiniatura = "fake-thumb.jpg", TipoEvidencia = CatalogosCaso.TiposEvidencia[0] }]
                });
                db.Oficios.Add(item);
            }
            await db.SaveChangesAsync();
            var rebuild = Stopwatch.StartNew();
            await Drain(db);
            output.WriteLine($"824 synthetic offices: refresh {rebuild.ElapsedMilliseconds} ms in bounded batches.");
            await AssertEquivalent(db);
            foreach (var enabled in new[] { false, true })
            {
                // Warm translation before timing repeated reads.
                await new ReportesModel(db, await Reader(db, enabled)).OnGetAsync();
                var watch = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) await new ReportesModel(db, await Reader(db, enabled)).OnGetAsync();
                output.WriteLine($"Reportes summary={enabled}: mean {watch.ElapsedMilliseconds / 5.0:N1} ms (local synthetic data).");
            }
        }
        finally
        {
            // The only DROP target is the random database created above.
            Assert.StartsWith("Bitacora_ReportTest_", database);
            await using var drop = new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Task<int> Pending(AppDbContext db) => db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.ReportChangeQueue").SingleAsync();

    private static async Task Drain(AppDbContext db)
    {
        var attempts = 0;
        do
        {
            Assert.True(++attempts <= 100);
            await db.Database.ExecuteSqlRawAsync("EXEC dbo.RefreshReportSummaries @BatchSize=25");
        } while (await Pending(db) > 0);
    }

    private static async Task<ReportReadService> Reader(AppDbContext db, bool enabled)
    {
        var reader = new ReportReadService(db, new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Reports:SummariesEnabled"] = enabled.ToString() }).Build());
        await reader.InitializeAsync(default);
        return reader;
    }

    private static async Task AssertEquivalent(AppDbContext db)
    {
        var direct = await Reader(db, false);
        var summary = await Reader(db, true);
        Assert.True(summary.UsesSummary);
        Assert.Equal(JsonSerializer.Serialize(await direct.Cases.OrderBy(c => c.Id).ToListAsync()),
            JsonSerializer.Serialize(await summary.Cases.OrderBy(c => c.Id).ToListAsync()));
        Assert.Equal(JsonSerializer.Serialize(await direct.Offices.OrderBy(c => c.Id).ToListAsync()),
            JsonSerializer.Serialize(await summary.Offices.OrderBy(c => c.Id).ToListAsync()));
        var livePage = new ReportesModel(db, direct);
        var summaryPage = new ReportesModel(db, summary);
        await livePage.OnGetAsync();
        await summaryPage.OnGetAsync();
        Assert.Equal(JsonSerializer.Serialize(livePage.StatCards), JsonSerializer.Serialize(summaryPage.StatCards));
        Assert.Equal(livePage.TotalCasosConEvidenciaIncorrecta, summaryPage.TotalCasosConEvidenciaIncorrecta);
        Assert.Equal(livePage.TotalPendientesEvidencia, summaryPage.TotalPendientesEvidencia);
        Assert.Equal(JsonSerializer.Serialize(livePage.CasosPorTipoCorreccion), JsonSerializer.Serialize(summaryPage.CasosPorTipoCorreccion));
        var liveExecutive = new EjecutivoModel(db, direct) { FechaInicio = new(2026, 1, 1), FechaFin = new(2026, 12, 31) };
        var summaryExecutive = new EjecutivoModel(db, summary) { FechaInicio = new(2026, 1, 1), FechaFin = new(2026, 12, 31) };
        await liveExecutive.OnGetAsync(default);
        await summaryExecutive.OnGetAsync(default);
        Assert.Equal(JsonSerializer.Serialize(liveExecutive.StatCards), JsonSerializer.Serialize(summaryExecutive.StatCards));
        Assert.Equal(JsonSerializer.Serialize(liveExecutive.ResumenMensual), JsonSerializer.Serialize(summaryExecutive.ResumenMensual));
        Assert.Equal(JsonSerializer.Serialize(liveExecutive.OficiosPorTipoCorreccion), JsonSerializer.Serialize(summaryExecutive.OficiosPorTipoCorreccion));
    }
}
