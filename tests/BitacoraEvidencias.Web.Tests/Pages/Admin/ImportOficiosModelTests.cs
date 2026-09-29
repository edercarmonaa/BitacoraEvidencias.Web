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

public class ImportOficiosModelTests
{
    [Fact]
    public async Task OnPostPreviewAsync_InvalidHeaders_ReturnsPageWithBlockingErrors()
    {
        await using var fixture = await ImportOficiosFixture.CreateAsync();
        fixture.Parser.Result = new TabularImportFile
        {
            FileName = "oficios.csv",
            Format = "csv",
            TotalRows = 1,
            Headers = { "Numero", "Fecha", "Asunto", "Notas" }
        };

        var model = fixture.CreateModel();
        model.Input = new ImportOficiosModel.UploadInput
        {
            File = CreateFormFile("oficios.csv")
        };

        var result = await model.OnPostPreviewAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Preview);
        Assert.False(model.Preview!.CanImport);
        Assert.Contains(model.Preview.Errors, error => error.Message.Contains("encabezados", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, fixture.PreviewStore.SaveCount);
    }

    [Fact]
    public async Task OnPostImportAsync_WithoutStoredPreview_RedirectsWithFlashError()
    {
        await using var fixture = await ImportOficiosFixture.CreateAsync();
        var model = fixture.CreateModel();

        var result = await model.OnPostImportAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Contains("vista previa expiro", model.FlashError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnPostImportAsync_PartialImport_PersistsValidRows_AndStoresRetryPreview()
    {
        await using var fixture = await ImportOficiosFixture.CreateAsync();
        fixture.DbContext.Oficios.Add(new Oficio
        {
            NumeroOficio = "OF-EXISTENTE",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Existente"
        });
        await fixture.DbContext.SaveChangesAsync();

        fixture.PreviewStore.StoredPreview = new ImportOficiosModel.OficioImportPreview
        {
            FileName = "oficios.csv",
            TotalRows = 2,
            Rows =
            {
                new ImportOficiosModel.OficioImportRow
                {
                    SourceRowNumber = 2,
                    NumeroOficio = "OF-001",
                    FechaOficio = new DateTime(2026, 3, 1),
                    Asunto = "Nuevo oficio"
                },
                new ImportOficiosModel.OficioImportRow
                {
                    SourceRowNumber = 3,
                    NumeroOficio = "OF-EXISTENTE",
                    FechaOficio = new DateTime(2026, 3, 2),
                    Asunto = "Duplicado"
                }
            }
        };

        var model = fixture.CreateModel();

        var result = await model.OnPostImportAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Contains("Se importaron 1 oficio", model.FlashSuccess!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 fila(s) pendientes", model.FlashError!, StringComparison.OrdinalIgnoreCase);

        var imported = await fixture.DbContext.Oficios.SingleAsync(x => x.NumeroOficio == "OF-001");
        Assert.Equal("Nuevo oficio", imported.Asunto);
        Assert.Equal(1, fixture.PreviewStore.SaveCount);
        Assert.NotNull(fixture.PreviewStore.StoredPreview);
        Assert.Empty(fixture.PreviewStore.StoredPreview!.Rows);
        Assert.Contains(fixture.PreviewStore.StoredPreview.Errors, error => error.Message.Contains("ya existe en la base", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fixture.AuditLogService.Entries, entry => entry.EventType == AuditEvents.Import);
    }

    private static IFormFile CreateFormFile(string fileName)
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }

    private sealed class ImportOficiosFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ImportOficiosFixture(
            SqliteConnection connection,
            AppDbContext dbContext,
            FakeTabularImportFileParser parser,
            MemoryImportPreviewStore<ImportOficiosModel.OficioImportPreview> previewStore,
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
        public MemoryImportPreviewStore<ImportOficiosModel.OficioImportPreview> PreviewStore { get; }
        public RecordingAuditLogService AuditLogService { get; }

        public static async Task<ImportOficiosFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new ImportOficiosFixture(
                connection,
                dbContext,
                new FakeTabularImportFileParser(),
                new MemoryImportPreviewStore<ImportOficiosModel.OficioImportPreview>(),
                new RecordingAuditLogService());
        }

        public ImportOficiosModel CreateModel()
        {
            var oficioImportApplicationService = new OficioImportApplicationService(DbContext, AuditLogService);
            var oficioImportPreviewService = new OficioImportPreviewService(DbContext, Parser);
            return new ImportOficiosModel(PreviewStore, oficioImportApplicationService, oficioImportPreviewService)
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
                new Claim(ClaimTypes.NameIdentifier, "7"),
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
