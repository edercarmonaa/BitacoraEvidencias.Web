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

public class ImportEvidenciasModelTests
{
    [Fact]
    public async Task OnPostPreviewAsync_InvalidHeaders_ReturnsPageWithBlockingErrors()
    {
        await using var fixture = await ImportEvidenciasFixture.CreateAsync();
        fixture.Parser.Result = new TabularImportFile
        {
            FileName = "evidencias.csv",
            Format = "csv",
            TotalRows = 1,
            Headers = { "OFICIO", "ARCHIVO" }
        };

        var model = fixture.CreateModel();
        model.Input = new ImportEvidenciasModel.UploadInput
        {
            File = CreateFormFile("evidencias.csv")
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
        await using var fixture = await ImportEvidenciasFixture.CreateAsync();
        var model = fixture.CreateModel();

        var result = await model.OnPostImportAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Contains("vista previa expiro", model.FlashError!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnPostImportAsync_PartialImport_PersistsEvidence_AndStoresRetryPreview()
    {
        await using var fixture = await ImportEvidenciasFixture.CreateAsync();
        var oficio = new Oficio
        {
            NumeroOficio = "OF-001",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Oficio base"
        };
        fixture.DbContext.Oficios.Add(oficio);
        await fixture.DbContext.SaveChangesAsync();

        var caso = new CasoCorreccion
        {
            OficioId = oficio.Id,
            Consecutivo = 1,
            Cct = "CCT001",
            Curp = "CURP00000000000001",
            NombreCompleto = "Caso base",
            PeriodoInicio = 2020,
            PeriodoFin = 2021,
            FechaVerificacion = new DateTime(2026, 2, 1),
            NivelEducativo = CatalogosCaso.NivelesEducativos[0],
            TipoCorreccion = "Correccion",
            Estatus = CatalogosCaso.Estatus[0],
            AuditoriaEstado = CatalogosAuditoriaCaso.Coincidente
        };
        fixture.DbContext.CasosCorreccion.Add(caso);
        await fixture.DbContext.SaveChangesAsync();

        fixture.PreviewStore.StoredPreview = new ImportEvidenciasModel.EvidenciaImportPreview
        {
            FileName = "evidencias.csv",
            TotalRows = 2,
            Rows =
            {
                new ImportEvidenciasModel.EvidenciaImportRow
                {
                    SourceRowNumber = 2,
                    NumeroOficio = "OF-001",
                    SourceFilePath = "C:\\temp\\evidencia-1.jpg",
                    TipoEvidencia = CatalogosCaso.TiposEvidencia[0],
                    NivelEducativo = CatalogosCaso.NivelesEducativos[0],
                    FechaEvidencia = new DateTime(2026, 3, 1),
                    Notas = "Primera",
                    TargetConsecutivo = 1
                },
                new ImportEvidenciasModel.EvidenciaImportRow
                {
                    SourceRowNumber = 3,
                    NumeroOficio = "OF-001",
                    SourceFilePath = "C:\\temp\\evidencia-2.jpg",
                    TipoEvidencia = CatalogosCaso.TiposEvidencia[1],
                    NivelEducativo = CatalogosCaso.NivelesEducativos[1],
                    FechaEvidencia = new DateTime(2026, 3, 2),
                    Notas = "Segunda",
                    TargetConsecutivo = 999
                }
            }
        };

        var model = fixture.CreateModel();

        var result = await model.OnPostImportAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Contains("Se importaron 1 evidencia", model.FlashSuccess!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 fila(s) pendientes", model.FlashError!, StringComparison.OrdinalIgnoreCase);

        var evidencia = await fixture.DbContext.EvidenciasFoto.SingleAsync();
        Assert.Equal(caso.Id, evidencia.CasoCorreccionId);
        Assert.Equal(CatalogosCaso.TiposEvidencia[0], evidencia.TipoEvidencia);
        Assert.Equal("stored/file-1.jpg", evidencia.RutaArchivo);
        Assert.Single(fixture.PhotoStorageService.SaveFromPathCalls);
        Assert.Single(fixture.CaseAuditService.ResetCalls);
        Assert.Single(fixture.CaseAuditService.WriteResetLogCalls);
        Assert.Equal(1, fixture.PreviewStore.SaveCount);
        Assert.NotNull(fixture.PreviewStore.StoredPreview);
        Assert.Empty(fixture.PreviewStore.StoredPreview!.Rows);
        Assert.Contains(fixture.PreviewStore.StoredPreview.Errors, error => error.Message.Contains("no tiene un caso", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(fixture.AuditLogService.Entries, entry => entry.EventType == AuditEvents.Import);
    }

    private static IFormFile CreateFormFile(string fileName)
    {
        var stream = new MemoryStream([1, 2, 3]);
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }

    private sealed class ImportEvidenciasFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ImportEvidenciasFixture(
            SqliteConnection connection,
            AppDbContext dbContext,
            FakeTabularImportFileParser parser,
            MemoryImportPreviewStore<ImportEvidenciasModel.EvidenciaImportPreview> previewStore,
            FakePhotoStorageService photoStorageService,
            RecordingAuditLogService auditLogService,
            FakeCaseAuditService caseAuditService)
        {
            _connection = connection;
            DbContext = dbContext;
            Parser = parser;
            PreviewStore = previewStore;
            PhotoStorageService = photoStorageService;
            AuditLogService = auditLogService;
            CaseAuditService = caseAuditService;
        }

        public AppDbContext DbContext { get; }
        public FakeTabularImportFileParser Parser { get; }
        public MemoryImportPreviewStore<ImportEvidenciasModel.EvidenciaImportPreview> PreviewStore { get; }
        public FakePhotoStorageService PhotoStorageService { get; }
        public RecordingAuditLogService AuditLogService { get; }
        public FakeCaseAuditService CaseAuditService { get; }

        public static async Task<ImportEvidenciasFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new ImportEvidenciasFixture(
                connection,
                dbContext,
                new FakeTabularImportFileParser(),
                new MemoryImportPreviewStore<ImportEvidenciasModel.EvidenciaImportPreview>(),
                new FakePhotoStorageService(),
                new RecordingAuditLogService(),
                new FakeCaseAuditService());
        }
public ImportEvidenciasModel CreateModel()
{
    var evidenciaImportApplicationService = new EvidenciaImportApplicationService(
        DbContext,
        PhotoStorageService,
        AuditLogService,
        CaseAuditService);
    var evidenciaImportPreviewService = new EvidenciaImportPreviewService(
        DbContext,
        Parser,
        PhotoStorageService);

    return new ImportEvidenciasModel(
    DbContext,
    PreviewStore,
    PhotoStorageService,
    evidenciaImportApplicationService,
    evidenciaImportPreviewService)

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
                new Claim(ClaimTypes.NameIdentifier, "11"),
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

    private sealed class FakePhotoStorageService : IPhotoStorageService
    {
        private int _saveCounter;

        public List<string> SaveFromPathCalls { get; } = [];

        public Task<StoredPhotoInfo> SaveEvidenceAsync(IFormFile file, string numeroOficio, int consecutivo, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StoredPhotoInfo> SaveEvidenceFromPathAsync(string sourceFilePath, string numeroOficio, int consecutivo, CancellationToken cancellationToken = default)
        {
            SaveFromPathCalls.Add(sourceFilePath);
            _saveCounter++;
            return Task.FromResult(new StoredPhotoInfo
            {
                RelativeFilePath = $"stored/file-{_saveCounter}.jpg",
                RelativeThumbnailPath = $"stored/thumb-{_saveCounter}.jpg"
            });
        }

        public bool TryValidateSourceEvidenceFile(string sourceFilePath, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryOpenReadEvidence(string relativePath, out Stream? stream, out string contentType)
        {
            stream = null;
            contentType = "application/octet-stream";
            return false;
        }

        public bool TryProtectExistingEvidenceFile(string relativePath, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryProtectExistingOficioFolder(string numeroOficio, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public void DeleteEvidenceFiles(string? relativeFilePath, string? relativeThumbnailPath)
        {
        }

        public void DeleteCaseFolder(string numeroOficio, int consecutivo)
        {
        }

        public void DeleteOficioFolder(string numeroOficio)
        {
        }

        public bool TryStageDeleteEvidenceFiles(string? relativeFilePath, string? relativeThumbnailPath, out StagedEvidenceDeletion? mutation, out string? errorMessage)
        {
            mutation = null;
            errorMessage = null;
            return true;
        }

        public bool TryRollbackStagedEvidenceDeletion(StagedEvidenceDeletion? mutation, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryFinalizeStagedEvidenceDeletion(StagedEvidenceDeletion? mutation, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryStageDeleteOficioFolder(string numeroOficio, out StagedOficioFolderDeletion? mutation, out string? errorMessage)
        {
            mutation = null;
            errorMessage = null;
            return true;
        }

        public bool TryRollbackStagedOficioFolderDeletion(StagedOficioFolderDeletion? mutation, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryFinalizeStagedOficioFolderDeletion(StagedOficioFolderDeletion? mutation, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryStageDeleteAndReorderCaseFolders(string numeroOficio, int deletedConsecutivo, IReadOnlyCollection<(int OldConsecutivo, int NewConsecutivo)> moves, out StagedCaseFolderMutation? mutation, out string? errorMessage)
        {
            mutation = null;
            errorMessage = null;
            return true;
        }

        public bool TryRollbackStagedCaseFolderMutation(StagedCaseFolderMutation? mutation, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryFinalizeStagedCaseFolderMutation(StagedCaseFolderMutation? mutation, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryRenameOficioFolder(string oldNumeroOficio, string newNumeroOficio, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public bool TryReorderCaseFolders(string numeroOficio, IReadOnlyCollection<(int OldConsecutivo, int NewConsecutivo)> moves, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public string RebaseRelativePathForCase(string relativePath, string numeroOficio, int oldConsecutivo, int newConsecutivo) => relativePath;

        public string RebaseRelativePathForOficio(string relativePath, string oldNumeroOficio, string newNumeroOficio) => relativePath;
    }

    private sealed class FakeCaseAuditService : ICaseAuditService
    {
        public List<int> ResetCalls { get; } = [];
        public List<int> WriteResetLogCalls { get; } = [];

        public void MarkCoincidente(CasoCorreccion caso)
        {
        }

        public void MarkNoCoincidente(CasoCorreccion caso, string observacion)
        {
        }

        public bool ResetToPending(CasoCorreccion caso)
        {
            ResetCalls.Add(caso.Id);
            caso.AuditoriaEstado = CatalogosAuditoriaCaso.Pendiente;
            return true;
        }

        public Task WriteDecisionLogAsync(CasoCorreccion caso, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task WriteResetLogAsync(CasoCorreccion caso, string details, CancellationToken cancellationToken = default)
        {
            WriteResetLogCalls.Add(caso.Id);
            return Task.CompletedTask;
        }
    }
}
