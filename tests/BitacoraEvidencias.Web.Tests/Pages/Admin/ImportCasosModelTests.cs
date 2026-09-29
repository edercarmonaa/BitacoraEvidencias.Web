using System.Security.Claims;
using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Admin;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using BitacoraEvidencias.Web.Tests.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Pages.Admin;

public class ImportCasosModelTests
{
    [Fact]
    public async Task OnPostPreviewAsync_InvalidHeaders_ReturnsPageWithBlockingErrors()
    {
        await using var fixture = await ImportCasosFixture.CreateAsync();
        fixture.Parser.Result = new TabularImportFile
        {
            FileName = "casos.csv",
            Format = "csv",
            TotalRows = 1,
            Headers = { "Oficio", "Folio" }
        };

        var model = fixture.CreateModel();
        model.Input = new ImportCasosModel.UploadInput
        {
            File = CreateFormFile("casos.csv")
        };

        var result = await model.OnPostPreviewAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Preview);
        Assert.False(model.Preview!.CanImport);
        Assert.Contains(model.Preview.Errors, error => error.Message.Contains("encabezados", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, fixture.PreviewStore.SaveCount);
    }

    [Fact]
    public async Task OnPostPreviewAsync_RowWithMissingOffice_AddsRowError()
    {
        await using var fixture = await ImportCasosFixture.CreateAsync();
        fixture.Parser.Result = BuildImportFile(
            [
                BuildRow(2, ["OF-404", "FOL-1", "CCT001", "2020", "2021", "CURP00000000000001", "Alumno Uno", "01/03/2026", "Primaria", "Correccion", "Obs", "Pendiente", "https://example.com/evidencia", "Validador Uno", "10"])
            ]);

        var model = fixture.CreateModel();
        model.Input = new ImportCasosModel.UploadInput
        {
            File = CreateFormFile("casos.csv")
        };

        var result = await model.OnPostPreviewAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Preview);
        Assert.Empty(model.Preview!.Rows);
        Assert.Contains(model.Preview.Errors, error => error.Message.Contains("oficio no existe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OnPostPreviewAsync_RowWithMissingAuditorId_AddsRowError()
    {
        await using var fixture = await ImportCasosFixture.CreateAsync();
        fixture.DbContext.Oficios.Add(new Oficio
        {
            NumeroOficio = "OF-001",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Oficio base"
        });
        await fixture.DbContext.SaveChangesAsync();

        fixture.Parser.Result = BuildImportFile(
            [
                BuildRow(2, ["OF-001", "FOL-1", "CCT001", "2020", "2021", "CURP00000000000001", "Alumno Uno", "01/03/2026", "Primaria", "Correccion", "Obs", "Pendiente", "https://example.com/evidencia", "Validador Uno", ""])
            ]);

        var model = fixture.CreateModel();
        model.Input = new ImportCasosModel.UploadInput
        {
            File = CreateFormFile("casos.csv")
        };

        var result = await model.OnPostPreviewAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Preview);
        Assert.Empty(model.Preview!.Rows);
        Assert.Contains(model.Preview.Errors, error => error.Message.Contains("AuditorId es obligatorio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OnPostImportAsync_PartialImport_PersistsValidCase_AndStoresRetryPreview()
    {
        await using var fixture = await ImportCasosFixture.CreateAsync();
        var oficio = new Oficio
        {
            NumeroOficio = "OF-001",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Oficio base"
        };
        fixture.DbContext.Oficios.Add(oficio);
        fixture.DbContext.UsuariosSistema.Add(new UsuarioSistema
        {
            Usuario = "auditor.activo",
            UsuarioNormalizado = "AUDITOR.ACTIVO",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rol = SecurityDefaults.AdminRole,
            Activo = true,
            MustChangePassword = false,
            CreadoEnUtc = DateTime.UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();
        var auditorId = await fixture.DbContext.UsuariosSistema
            .Where(x => x.Usuario == "auditor.activo")
            .Select(x => x.Id)
            .SingleAsync();

        fixture.DbContext.CasosCorreccion.Add(new CasoCorreccion
        {
            OficioId = oficio.Id,
            Consecutivo = 1,
            Folio = "FOL-EXISTE",
            Cct = "CCT001",
            PeriodoInicio = 2020,
            PeriodoFin = 2021,
            Curp = "CURP00000000000001",
            NombreCompleto = "Caso existente",
            FechaVerificacion = new DateTime(2026, 1, 10),
            NivelEducativo = CatalogosCaso.NivelesEducativos[1],
            TipoCorreccion = "Correccion previa",
            Estatus = CatalogosCaso.Estatus[0]
        });
        await fixture.DbContext.SaveChangesAsync();

        fixture.PreviewStore.StoredPreview = new ImportCasosModel.CasoImportPreview
        {
            FileName = "casos.csv",
            TotalRows = 2,
            Rows =
            {
                new ImportCasosModel.CasoImportRow
                {
                    SourceRowNumber = 2,
                    OficioNumero = "OF-001",
                    Folio = "FOL-NEW",
                    Cct = "CCT002",
                    PeriodoInicio = 2021,
                    PeriodoFin = 2022,
                    Curp = "CURP00000000000002",
                    NombreCompleto = "Caso nuevo",
                    FechaVerificacion = new DateTime(2026, 3, 1),
                    NivelEducativo = CatalogosCaso.NivelesEducativos[0],
                    TipoCorreccion = "Correccion nueva",
                    Observaciones = "Obs",
                    Estatus = CatalogosCaso.Estatus[0],
                    EvidenciaUrl = "https://example.com/uno",
                    Validador = "Validador Uno",
                    AuditorUsuarioId = auditorId,
                    AuditorUsuario = "auditor.activo"
                },
                new ImportCasosModel.CasoImportRow
                {
                    SourceRowNumber = 3,
                    OficioNumero = "OF-001",
                    Folio = "FOL-DUP",
                    Cct = "CCT003",
                    PeriodoInicio = 2021,
                    PeriodoFin = 2022,
                    Curp = "CURP00000000000003",
                    NombreCompleto = "Caso duplicado",
                    FechaVerificacion = new DateTime(2026, 3, 2),
                    NivelEducativo = CatalogosCaso.NivelesEducativos[1],
                    TipoCorreccion = "Correccion duplicada",
                    Observaciones = "Obs",
                    Estatus = CatalogosCaso.Estatus[0],
                    EvidenciaUrl = "https://example.com/dos",
                    Validador = "Validador Dos",
                    AuditorUsuarioId = auditorId,
                    AuditorUsuario = "auditor.activo"
                }
            }
        };

        var model = fixture.CreateModel();

        var result = await model.OnPostImportAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Contains("Se importaron 1 caso", model.FlashSuccess!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 fila(s) pendientes", model.FlashError!, StringComparison.OrdinalIgnoreCase);

        var importedCase = await fixture.DbContext.CasosCorreccion.SingleAsync(x => x.Folio == "FOL-NEW");
        Assert.Equal(oficio.Id, importedCase.OficioId);
        Assert.Equal(2, importedCase.Consecutivo);
        Assert.Equal(CatalogosCaso.NivelesEducativos[0], importedCase.NivelEducativo);
        Assert.Equal("Validador Uno", importedCase.Validador);
        Assert.Equal(auditorId, importedCase.AuditorUsuarioId);
        Assert.Equal(1, fixture.PreviewStore.SaveCount);
        Assert.NotNull(fixture.PreviewStore.StoredPreview);
        Assert.Empty(fixture.PreviewStore.StoredPreview!.Rows);
        Assert.Contains(fixture.PreviewStore.StoredPreview.Errors, error => error.Message.Contains("ya tiene un caso", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fixture.AuditLogService.Entries, entry => entry.EventType == AuditEvents.Import);
    }

    private static TabularImportFile BuildImportFile(IReadOnlyCollection<TabularImportRow> rows)
    {
        var file = new TabularImportFile
        {
            FileName = "casos.csv",
            Format = "csv",
            TotalRows = rows.Count
        };

        file.Headers.AddRange([
            "Oficio",
            "Folio",
            "Cct",
            "PeriodoInicio",
            "PeriodoFin",
            "Curp",
            "NombreCompleto",
            "FechaVerificacion",
            "NIVEL",
            "TipoCorreccion",
            "Observaciones",
            "Estatus",
            "EvidenciaUrl",
            "Validador",
            "AuditorId"
        ]);
        file.Rows.AddRange(rows);
        return file;
    }

    private static TabularImportRow BuildRow(int rowNumber, IReadOnlyCollection<string> values)
    {
        var row = new TabularImportRow { SourceRowNumber = rowNumber };
        row.Values.AddRange(values);
        return row;
    }

    private static IFormFile CreateFormFile(string fileName)
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }

    private sealed class ImportCasosFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ImportCasosFixture(
            SqliteConnection connection,
            AppDbContext dbContext,
            FakeTabularImportFileParser parser,
            MemoryImportPreviewStore<ImportCasosModel.CasoImportPreview> previewStore,
            RecordingAuditLogService auditLogService)
        {
            _connection = connection;
            DbContext = dbContext;
            Parser = parser;
            PreviewStore = previewStore;
            AuditLogService = auditLogService;
        }

        public AppDbContext DbContext { get; }
        public FakeTabularImportFileParser Parser { get; }
        public MemoryImportPreviewStore<ImportCasosModel.CasoImportPreview> PreviewStore { get; }
        public RecordingAuditLogService AuditLogService { get; }

        public static async Task<ImportCasosFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new ImportCasosFixture(
                connection,
                dbContext,
                new FakeTabularImportFileParser(),
                new MemoryImportPreviewStore<ImportCasosModel.CasoImportPreview>(),
                new RecordingAuditLogService());
        }

        public ImportCasosModel CreateModel()
        {
            var casoImportApplicationService = new CasoImportApplicationService(DbContext, AuditLogService);
            var casoImportPreviewService = new CasoImportPreviewService(DbContext, Parser);
            return new ImportCasosModel(PreviewStore, casoImportApplicationService, casoImportPreviewService)

            {
                PageContext = new PageContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = BuildPrincipal()
                    }
                }
            };
        }

        private static ClaimsPrincipal BuildPrincipal()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "9"),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, SecurityDefaults.AdminRole)
            ],
            "TestAuth");

            return new ClaimsPrincipal(identity);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}

