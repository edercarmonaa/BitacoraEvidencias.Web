using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages;
using BitacoraEvidencias.Web.Pages.Reportes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Pages;

public class ReportesModelTests
{
    [Fact]
    public async Task OnGetAsync_CountsDistinctOficiosByTipoCorreccion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var oficioUno = new Oficio
        {
            NumeroOficio = "OF-001",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Oficio con casos repetidos"
        };
        var oficioDos = new Oficio
        {
            NumeroOficio = "OF-002",
            FechaOficio = new DateTime(2026, 1, 2),
            Asunto = "Oficio adicional"
        };

        dbContext.Oficios.AddRange(oficioUno, oficioDos);
        await dbContext.SaveChangesAsync();

        dbContext.CasosCorreccion.AddRange(
            CreateCaso(oficioUno.Id, 1, CatalogosCaso.NivelesEducativos[0], "CORRECCION DE CURP", "Completado"),
            CreateCaso(oficioUno.Id, 2, CatalogosCaso.NivelesEducativos[1], "CORRECCION DE CURP", "Completado"),
            CreateCaso(oficioDos.Id, 1, CatalogosCaso.NivelesEducativos[0], "CORRECCION DE CURP", "Completado"),
            CreateCaso(oficioDos.Id, 2, CatalogosCaso.NivelesEducativos[1], "CORRECCION DE NOMBRE", "Pendiente"));
        await dbContext.SaveChangesAsync();

        var model = new ReportesModel(dbContext);

        await model.OnGetAsync();

        var curpRow = Assert.Single(model.CasosPorTipoCorreccion, x => x.TipoCorreccion == "CORRECCION DE CURP");
        Assert.Equal(2, curpRow.Total);
        Assert.DoesNotContain(model.CasosPorTipoCorreccion, x => x.TipoCorreccion == "CORRECCION DE NOMBRE");
        var noCompletado = Assert.Single(model.CasosNoCompletados);
        Assert.Equal("CORRECCION DE NOMBRE", noCompletado.TipoCorreccion);
        Assert.Equal("Pendiente", noCompletado.Estatus);
    }

    [Fact]
    public async Task CasosNoCompletadosModel_OnGetAsync_ReturnsOnlyNonCompletedCases()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var oficio = new Oficio
        {
            NumeroOficio = "OF-001",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Oficio base"
        };

        dbContext.Oficios.Add(oficio);
        await dbContext.SaveChangesAsync();

        dbContext.CasosCorreccion.AddRange(
            CreateCaso(oficio.Id, 1, CatalogosCaso.NivelesEducativos[0], "CORRECCION DE CURP", "Completado"),
            CreateCaso(oficio.Id, 2, CatalogosCaso.NivelesEducativos[1], "CORRECCION DE NOMBRE", "En proceso"));
        await dbContext.SaveChangesAsync();

        var model = new CasosNoCompletadosModel(dbContext);

        await model.OnGetAsync();

        var item = Assert.Single(model.Items);
        Assert.Equal("CORRECCION DE NOMBRE", item.TipoCorreccion);
        Assert.Equal("En proceso", item.Estatus);
        Assert.Equal(1, model.TotalRows);
    }

    [Fact]
    public async Task OnGetAsync_LimitsTipoCorreccionPreviewToFiveMostImportant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO A", 6);
        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO B", 5);
        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO C", 4);
        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO D", 3);
        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO E", 2);
        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO F", 1);

        var model = new ReportesModel(dbContext);

        await model.OnGetAsync();

        Assert.Equal(6, model.TotalTiposCorreccionCompletados);
        Assert.Equal(5, model.CasosPorTipoCorreccion.Count);
        Assert.Equal(["TIPO A", "TIPO B", "TIPO C", "TIPO D", "TIPO E"], model.CasosPorTipoCorreccion.Select(x => x.TipoCorreccion));
        Assert.DoesNotContain(model.CasosPorTipoCorreccion, x => x.TipoCorreccion == "TIPO F");
    }

    [Fact]
    public async Task OnGetAsync_LimitsPendientesEvidenciaPreviewToFive()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        await SeedPendingEvidenceCasesAsync(dbContext, 6);

        var model = new ReportesModel(dbContext);

        await model.OnGetAsync();

        Assert.Equal(6, model.TotalPendientesEvidencia);
        Assert.Equal(5, model.PendientesEvidencia.Count);
    }


    [Fact]
    public async Task OficiosPorTipoCorreccionModel_OnGetAsync_ReturnsCompleteGroupedList()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO A", 2);
        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO B", 1);
        await SeedCompletedOficiosByTipoAsync(dbContext, "TIPO C", 1);

        var model = new OficiosPorTipoCorreccionModel(dbContext);

        await model.OnGetAsync();

        Assert.Equal(3, model.TotalRows);
        Assert.Equal(["TIPO A", "TIPO B", "TIPO C"], model.Items.Select(x => x.TipoCorreccion));
        Assert.Equal([2, 1, 1], model.Items.Select(x => x.Total));
    }

    [Fact]
    public async Task EjecutivoModel_OnGetAsync_ReturnsExecutiveSummaryForVerificationPeriod()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var oficioUno = new Oficio
        {
            NumeroOficio = "OF-EJ-001",
            FechaOficio = new DateTime(2026, 2, 1),
            Asunto = "Oficio ejecutivo uno"
        };
        var oficioDos = new Oficio
        {
            NumeroOficio = "OF-EJ-002",
            FechaOficio = new DateTime(2026, 2, 2),
            Asunto = "Oficio ejecutivo dos"
        };
        var oficioFueraPeriodo = new Oficio
        {
            NumeroOficio = "OF-EJ-003",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Oficio fuera de periodo"
        };

        dbContext.Oficios.AddRange(oficioUno, oficioDos, oficioFueraPeriodo);
        await dbContext.SaveChangesAsync();

        var casoPrimariaPendiente = CreateCaso(
            oficioUno.Id,
            1,
            CatalogosCaso.NivelesEducativos[0],
            "CORRECCION DE CURP",
            CatalogosCaso.Estatus[0]);
        casoPrimariaPendiente.FechaVerificacion = new DateTime(2026, 2, 10);
        casoPrimariaPendiente.EvidenciaUrl = "https://example.test/evidencia.jpg";

        var casoSecundariaCompletado = CreateCaso(
            oficioUno.Id,
            2,
            CatalogosCaso.NivelesEducativos[1],
            "CORRECCION DE NOMBRE",
            CatalogosCaso.EstatusCompletado);
        casoSecundariaCompletado.FechaVerificacion = new DateTime(2026, 2, 11);

        var casoOficioDosPendiente = CreateCaso(
            oficioDos.Id,
            1,
            CatalogosCaso.NivelesEducativos[0],
            "CORRECCION DE CURP",
            "En proceso");
        casoOficioDosPendiente.FechaVerificacion = new DateTime(2026, 2, 15);

        var casoFueraPeriodo = CreateCaso(
            oficioFueraPeriodo.Id,
            1,
            CatalogosCaso.NivelesEducativos[0],
            "CORRECCION DE CURP",
            CatalogosCaso.EstatusCompletado);
        casoFueraPeriodo.FechaVerificacion = new DateTime(2026, 1, 15);

        dbContext.CasosCorreccion.AddRange(
            casoPrimariaPendiente,
            casoSecundariaCompletado,
            casoOficioDosPendiente,
            casoFueraPeriodo);
        await dbContext.SaveChangesAsync();

        dbContext.EvidenciasFoto.AddRange(
            CreateEvidencia(casoPrimariaPendiente.Id),
            CreateEvidencia(casoSecundariaCompletado.Id),
            CreateEvidencia(casoFueraPeriodo.Id));
        await dbContext.SaveChangesAsync();

        var model = new EjecutivoModel(dbContext)
        {
            FechaInicio = new DateTime(2026, 2, 1),
            FechaFin = new DateTime(2026, 2, 28)
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(2, model.TotalOficiosVerificados);
        Assert.Equal(3, model.TotalCasosVerificados);
        Assert.Equal(3, model.TotalEvidencias);
        Assert.Equal(2, model.TotalCasosPendientes);

        Assert.Equal(2, model.CasosPorNivel.Single(x => x.NivelEducativo == CatalogosCaso.NivelesEducativos[0]).Total);
        Assert.Equal(1, model.CasosPorNivel.Single(x => x.NivelEducativo == CatalogosCaso.NivelesEducativos[1]).Total);
        Assert.Equal(2, model.OficiosPorTipoCorreccion.Single(x => x.TipoCorreccion == "CORRECCION DE CURP").TotalOficios);
        Assert.Equal(1, model.OficiosPorTipoCorreccion.Single(x => x.TipoCorreccion == "CORRECCION DE NOMBRE").TotalOficios);
        Assert.Equal(["OF-EJ-002", "OF-EJ-001"], model.CasosPendientes.Select(x => x.NumeroOficio));
        var febrero = Assert.Single(model.ResumenMensual);
        Assert.Equal(2026, febrero.Anio);
        Assert.Equal(2, febrero.Mes);
        Assert.Equal(2, febrero.TotalOficios);
        Assert.Equal(3, febrero.TotalCasos);
        var anioOficio = Assert.Single(model.OficiosPorAnioFechaOficio);
        Assert.Equal(2026, anioOficio.Anio);
        Assert.Equal(2, anioOficio.TotalOficios);
    }

    [Fact]
    public async Task EjecutivoModel_OnGetAsync_CountsMultipleCasesInSameOfficeAsOneOffice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var oficio = new Oficio
        {
            NumeroOficio = "OF-MULTI-001",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Oficio con multiples casos"
        };

        dbContext.Oficios.Add(oficio);
        await dbContext.SaveChangesAsync();

        var casoPrimaria = CreateCaso(
            oficio.Id,
            1,
            CatalogosCaso.NivelesEducativos[0],
            "CORRECCION DE CURP",
            CatalogosCaso.EstatusCompletado);
        casoPrimaria.FechaVerificacion = new DateTime(2026, 3, 1);

        var casoSecundaria = CreateCaso(
            oficio.Id,
            2,
            CatalogosCaso.NivelesEducativos[1],
            "CORRECCION DE NOMBRE",
            CatalogosCaso.EstatusCompletado);
        casoSecundaria.FechaVerificacion = new DateTime(2026, 3, 2);

        dbContext.CasosCorreccion.AddRange(casoPrimaria, casoSecundaria);
        await dbContext.SaveChangesAsync();

        var model = new EjecutivoModel(dbContext)
        {
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 12, 31)
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, model.TotalOficiosVerificados);
        Assert.Equal(2, model.TotalCasosVerificados);
    }

    [Fact]
    public async Task EjecutivoModel_OnGetAsync_AssignsOfficeToFirstVerificationPeriod()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var oficio = new Oficio
        {
            NumeroOficio = "OF-CROSS-001",
            FechaOficio = new DateTime(2025, 12, 15),
            Asunto = "Oficio con verificaciones en dos ejercicios"
        };

        dbContext.Oficios.Add(oficio);
        await dbContext.SaveChangesAsync();

        var casoVerificadoEn2025 = CreateCaso(
            oficio.Id,
            1,
            CatalogosCaso.NivelesEducativos[0],
            "CORRECCION DE CURP",
            CatalogosCaso.EstatusCompletado);
        casoVerificadoEn2025.FechaVerificacion = new DateTime(2025, 12, 20);

        var casoVerificadoEn2026 = CreateCaso(
            oficio.Id,
            2,
            CatalogosCaso.NivelesEducativos[1],
            "CORRECCION DE NOMBRE",
            CatalogosCaso.EstatusCompletado);
        casoVerificadoEn2026.FechaVerificacion = new DateTime(2026, 1, 5);

        dbContext.CasosCorreccion.AddRange(casoVerificadoEn2025, casoVerificadoEn2026);
        await dbContext.SaveChangesAsync();

        var model = new EjecutivoModel(dbContext)
        {
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 12, 31)
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(0, model.TotalOficiosVerificados);
        Assert.Equal(1, model.TotalCasosVerificados);
        var enero = Assert.Single(model.ResumenMensual, x => x.Anio == 2026 && x.Mes == 1);
        Assert.Equal(0, enero.TotalOficios);
        Assert.Equal(1, enero.TotalCasos);
        Assert.Empty(model.OficiosPorAnioFechaOficio);
    }

    private static CasoCorreccion CreateCaso(
        int oficioId,
        int consecutivo,
        string nivelEducativo,
        string tipoCorreccion,
        string estatus)
        => new()
        {
            OficioId = oficioId,
            Consecutivo = consecutivo,
            Curp = $"CURP{oficioId:000}{consecutivo:000}ABCDE",
            NombreCompleto = $"Caso {oficioId}-{consecutivo}",
            NivelEducativo = nivelEducativo,
            TipoCorreccion = tipoCorreccion,
            Estatus = estatus,
            AuditoriaEstado = CatalogosAuditoriaCaso.Pendiente
        };

    private static async Task SeedCompletedOficiosByTipoAsync(
        AppDbContext dbContext,
        string tipoCorreccion,
        int totalOficios)
    {
        for (var i = 1; i <= totalOficios; i++)
        {
            var suffix = $"{tipoCorreccion.Replace(" ", string.Empty)}-{i:000}";
            var oficio = new Oficio
            {
                NumeroOficio = $"OF-{suffix}",
                FechaOficio = new DateTime(2026, 1, 1).AddDays(i),
                Asunto = $"Oficio {suffix}"
            };

            dbContext.Oficios.Add(oficio);
            await dbContext.SaveChangesAsync();

            dbContext.CasosCorreccion.Add(CreateCaso(
                oficio.Id,
                1,
                CatalogosCaso.NivelesEducativos[0],
                tipoCorreccion,
                "Completado"));
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedPendingEvidenceCasesAsync(AppDbContext dbContext, int totalCases)
    {
        for (var i = 1; i <= totalCases; i++)
        {
            var oficio = new Oficio
            {
                NumeroOficio = $"OF-PEND-{i:000}",
                FechaOficio = new DateTime(2026, 1, 1).AddDays(i),
                Asunto = $"Oficio pendiente {i:000}"
            };

            dbContext.Oficios.Add(oficio);
            await dbContext.SaveChangesAsync();

            dbContext.CasosCorreccion.Add(CreateCaso(
                oficio.Id,
                1,
                CatalogosCaso.NivelesEducativos[0],
                "CORRECCION DE CURP",
                "Pendiente"));
        }

        await dbContext.SaveChangesAsync();
    }

    private static EvidenciaFoto CreateEvidencia(int casoId)
        => new()
        {
            CasoCorreccionId = casoId,
            RutaArchivo = $"oficios/evidencia-{casoId}.jpg",
            RutaMiniatura = $"oficios/evidencia-{casoId}-thumb.jpg",
            TipoEvidencia = CatalogosCaso.TiposEvidencia[2],
            FechaEvidencia = new DateTime(2026, 2, 12)
        };
}
