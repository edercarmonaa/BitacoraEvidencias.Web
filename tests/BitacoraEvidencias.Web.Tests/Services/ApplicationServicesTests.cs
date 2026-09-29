using BitacoraEvidencias.Web.Data;
using BitacoraEvidencias.Web.Models;
using BitacoraEvidencias.Web.Pages.Admin;
using BitacoraEvidencias.Web.Security;
using BitacoraEvidencias.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BitacoraEvidencias.Web.Tests.Services;

public class ApplicationServicesTests
{
    [Fact]
    public async Task UserAdministrationService_ToggleActiveAsync_PreventsSelfDeactivation()
    {
        await using var fixture = await ApplicationServiceFixture.CreateAsync();
        var admin = new UsuarioSistema
        {
            Usuario = "admin",
            UsuarioNormalizado = "ADMIN",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rol = SecurityDefaults.AdminRole,
            Activo = true,
            CreadoEnUtc = DateTime.UtcNow
        };

        fixture.DbContext.UsuariosSistema.Add(admin);
        await fixture.DbContext.SaveChangesAsync();

        var service = new UserAdministrationService(fixture.DbContext, fixture.AuditLogService);

        var result = await service.ToggleActiveAsync(admin.Id, admin.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("No puedes desactivar tu propio usuario.", result.ErrorMessage);
        Assert.True(await fixture.DbContext.UsuariosSistema.AnyAsync(x => x.Id == admin.Id && x.Activo));
    }

    [Fact]
    public async Task CasoApplicationService_UpdateAsync_AllowsCurrentInactiveAuditor()
    {
        await using var fixture = await ApplicationServiceFixture.CreateAsync();
        var inactiveAuditor = new UsuarioSistema
        {
            Usuario = "auditor.inactivo",
            UsuarioNormalizado = "AUDITOR.INACTIVO",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Rol = SecurityDefaults.CapturistaRole,
            Activo = false,
            CreadoEnUtc = DateTime.UtcNow
        };
        var oficio = new Oficio
        {
            NumeroOficio = "OF-001",
            FechaOficio = new DateTime(2026, 1, 10),
            Asunto = "Oficio base"
        };

        fixture.DbContext.UsuariosSistema.Add(inactiveAuditor);
        fixture.DbContext.Oficios.Add(oficio);
        await fixture.DbContext.SaveChangesAsync();

        var caso = new CasoCorreccion
        {
            OficioId = oficio.Id,
            Consecutivo = 1,
            Curp = "CURP00000000000001",
            NombreCompleto = "Caso original",
            NivelEducativo = CatalogosCaso.NivelesEducativos[0],
            TipoCorreccion = "Correccion",
            Estatus = CatalogosCaso.Estatus[0],
            AuditoriaEstado = CatalogosAuditoriaCaso.Coincidente,
            AuditorUsuarioId = inactiveAuditor.Id
        };

        fixture.DbContext.CasosCorreccion.Add(caso);
        await fixture.DbContext.SaveChangesAsync();

        var service = new CasoApplicationService(
            fixture.DbContext,
            fixture.PhotoStorageService,
            fixture.AuditLogService,
            fixture.CaseAuditService);

        var result = await service.UpdateAsync(new CasoUpdateRequest
        {
            Id = caso.Id,
            Curp = caso.Curp,
            NombreCompleto = "Caso actualizado",
            NivelEducativo = caso.NivelEducativo,
            TipoCorreccion = caso.TipoCorreccion,
            Estatus = CatalogosCaso.Estatus[1],
            AuditorUsuarioId = inactiveAuditor.Id
        });

        Assert.True(result.Succeeded);
        var storedCaso = await fixture.DbContext.CasosCorreccion.SingleAsync(x => x.Id == caso.Id);
        Assert.Equal(inactiveAuditor.Id, storedCaso.AuditorUsuarioId);
        Assert.Equal("Caso actualizado", storedCaso.NombreCompleto);
        Assert.Equal(CatalogosAuditoriaCaso.Pendiente, storedCaso.AuditoriaEstado);
        Assert.Single(fixture.CaseAuditService.ResetCalls);
        Assert.Single(fixture.CaseAuditService.WriteResetLogCalls);
    }

    [Fact]
    public async Task OficioApplicationService_UpdateAsync_RebasesEvidencePaths_WhenNumeroChanges()
    {
        await using var fixture = await ApplicationServiceFixture.CreateAsync();
        var oficio = new Oficio
        {
            NumeroOficio = "OF-001",
            FechaOficio = new DateTime(2026, 1, 10),
            Asunto = "Oficio base"
        };

        fixture.DbContext.Oficios.Add(oficio);
        await fixture.DbContext.SaveChangesAsync();

        var caso = new CasoCorreccion
        {
            OficioId = oficio.Id,
            Consecutivo = 1,
            NivelEducativo = CatalogosCaso.NivelesEducativos[0],
            TipoCorreccion = "Correccion",
            Estatus = CatalogosCaso.Estatus[0],
            AuditoriaEstado = CatalogosAuditoriaCaso.Coincidente
        };
        fixture.DbContext.CasosCorreccion.Add(caso);
        await fixture.DbContext.SaveChangesAsync();

        fixture.DbContext.EvidenciasFoto.Add(new EvidenciaFoto
        {
            CasoCorreccionId = caso.Id,
            RutaArchivo = "oficios/OF-001/001/file.jpg",
            RutaMiniatura = "oficios/OF-001/001/thumb.jpg",
            TipoEvidencia = CatalogosCaso.TiposEvidencia[0],
            FechaEvidencia = new DateTime(2026, 2, 1)
        });
        await fixture.DbContext.SaveChangesAsync();

        var service = new OficioApplicationService(
            fixture.DbContext,
            fixture.PhotoStorageService,
            fixture.AuditLogService,
            fixture.CaseAuditService);

        var result = await service.UpdateAsync(new OficioUpdateRequest
        {
            Id = oficio.Id,
            NumeroOficio = "OF-002",
            FechaOficio = oficio.FechaOficio,
            Asunto = oficio.Asunto
        });

        Assert.True(result.Succeeded);
        Assert.Equal(("OF-001", "OF-002"), Assert.Single(fixture.PhotoStorageService.RenameOficioCalls));

        var storedEvidence = await fixture.DbContext.EvidenciasFoto.SingleAsync();
        Assert.Equal("oficios/OF-002/001/file.jpg", storedEvidence.RutaArchivo);
        Assert.Equal("oficios/OF-002/001/thumb.jpg", storedEvidence.RutaMiniatura);
        Assert.Single(fixture.CaseAuditService.ResetCalls);
        Assert.Single(fixture.CaseAuditService.WriteResetLogCalls);
    }

    [Fact]
    public async Task EvidenciaApplicationService_UpdateAsync_ReplacesFile_AndDeletesPreviousFiles()
    {
        await using var fixture = await ApplicationServiceFixture.CreateAsync();
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
            NivelEducativo = CatalogosCaso.NivelesEducativos[0],
            TipoCorreccion = "Correccion",
            Estatus = CatalogosCaso.Estatus[0],
            AuditoriaEstado = CatalogosAuditoriaCaso.Coincidente
        };
        fixture.DbContext.CasosCorreccion.Add(caso);
        await fixture.DbContext.SaveChangesAsync();

        var evidencia = new EvidenciaFoto
        {
            CasoCorreccionId = caso.Id,
            RutaArchivo = "old/file.jpg",
            RutaMiniatura = "old/thumb.jpg",
            TipoEvidencia = CatalogosCaso.TiposEvidencia[1],
            FechaEvidencia = new DateTime(2026, 2, 1),
            Notas = "Anterior"
        };
        fixture.DbContext.EvidenciasFoto.Add(evidencia);
        await fixture.DbContext.SaveChangesAsync();

        var service = new EvidenciaApplicationService(
            fixture.DbContext,
            fixture.PhotoStorageService,
            fixture.AuditLogService,
            fixture.CaseAuditService);

        var fileStream = new MemoryStream([1, 2, 3]);
        var file = new FormFile(fileStream, 0, fileStream.Length, "archivo", "reemplazo.jpg");

        var result = await service.UpdateAsync(new EvidenciaUpdateRequest
        {
            Id = evidencia.Id,
            TipoEvidencia = evidencia.TipoEvidencia,
            FechaEvidencia = evidencia.FechaEvidencia,
            ArchivoReemplazo = file
        });

        Assert.True(result.Succeeded);
        var stored = await fixture.DbContext.EvidenciasFoto.SingleAsync(x => x.Id == evidencia.Id);
        Assert.Equal("stored/file-1.jpg", stored.RutaArchivo);
        Assert.Equal("stored/thumb-1.jpg", stored.RutaMiniatura);
        Assert.Equal(CatalogosCaso.TiposEvidencia[0], stored.TipoEvidencia);
        Assert.Null(stored.Notas);
        Assert.Contains(("old/file.jpg", "old/thumb.jpg"), fixture.PhotoStorageService.DeletedEvidenceFilesCalls);
        Assert.Single(fixture.CaseAuditService.ResetCalls);
        Assert.Single(fixture.CaseAuditService.WriteResetLogCalls);
        Assert.Contains(fixture.AuditLogService.Entries, entry => entry.EventType == AuditEvents.UploadEvidence);
    }

    [Fact]
    public async Task OficioImportApplicationService_ImportAsync_PersistsValidRows_AndReturnsDuplicateAsFailed()
    {
        await using var fixture = await ApplicationServiceFixture.CreateAsync();
        fixture.DbContext.Oficios.Add(new Oficio
        {
            NumeroOficio = "OF-001",
            FechaOficio = new DateTime(2026, 1, 1),
            Asunto = "Existente"
        });
        await fixture.DbContext.SaveChangesAsync();

        var service = new OficioImportApplicationService(fixture.DbContext, fixture.AuditLogService);
        var preview = new ImportOficiosModel.OficioImportPreview
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
                    Asunto = "Duplicado"
                },
                new ImportOficiosModel.OficioImportRow
                {
                    SourceRowNumber = 3,
                    NumeroOficio = "OF-002",
                    FechaOficio = new DateTime(2026, 3, 2),
                    Asunto = "Nuevo"
                }
            }
        };

        var result = await service.ImportAsync(preview);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.ImportedCount);
        Assert.Single(result.FailedRows);
        Assert.Equal("OF-001", result.FailedRows[0].NumeroOficio);
        Assert.True(await fixture.DbContext.Oficios.AnyAsync(x => x.NumeroOficio == "OF-002"));
        Assert.Contains(fixture.AuditLogService.Entries, entry => entry.EventType == AuditEvents.Import);
    }

    [Fact]
    public async Task EvidenciaImportApplicationService_ImportAsync_PersistsValidRows_AndReturnsFailures()
    {
        await using var fixture = await ApplicationServiceFixture.CreateAsync();
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
            Curp = "CURP00000000000001",
            NombreCompleto = "Caso base",
            NivelEducativo = CatalogosCaso.NivelesEducativos[0],
            TipoCorreccion = "Correccion",
            Estatus = CatalogosCaso.Estatus[0],
            AuditoriaEstado = CatalogosAuditoriaCaso.Coincidente
        };
        fixture.DbContext.CasosCorreccion.Add(caso);
        await fixture.DbContext.SaveChangesAsync();

        var service = new EvidenciaImportApplicationService(
            fixture.DbContext,
            fixture.PhotoStorageService,
            fixture.AuditLogService,
            fixture.CaseAuditService);

        var preview = new ImportEvidenciasModel.EvidenciaImportPreview
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
                    TargetConsecutivo = 999
                }
            }
        };

        var result = await service.ImportAsync(preview);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.ImportedCount);
        Assert.Single(result.FailedRows);
        var evidencia = await fixture.DbContext.EvidenciasFoto.SingleAsync();
        Assert.Equal(caso.Id, evidencia.CasoCorreccionId);
        Assert.Equal("stored/file-1.jpg", evidencia.RutaArchivo);
        Assert.Single(fixture.PhotoStorageService.SaveFromPathCalls);
        Assert.Single(fixture.CaseAuditService.ResetCalls);
        Assert.Single(fixture.CaseAuditService.WriteResetLogCalls);
        Assert.Contains(fixture.AuditLogService.Entries, entry => entry.EventType == AuditEvents.Import);
    }

    private sealed class ApplicationServiceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ApplicationServiceFixture(
            SqliteConnection connection,
            AppDbContext dbContext,
            RecordingAuditLogService auditLogService,
            FakePhotoStorageService photoStorageService,
            FakeCaseAuditService caseAuditService)
        {
            _connection = connection;
            DbContext = dbContext;
            AuditLogService = auditLogService;
            PhotoStorageService = photoStorageService;
            CaseAuditService = caseAuditService;
        }

        public AppDbContext DbContext { get; }
        public RecordingAuditLogService AuditLogService { get; }
        public FakePhotoStorageService PhotoStorageService { get; }
        public FakeCaseAuditService CaseAuditService { get; }

        public static async Task<ApplicationServiceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new ApplicationServiceFixture(
                connection,
                dbContext,
                new RecordingAuditLogService(),
                new FakePhotoStorageService(),
                new FakeCaseAuditService());
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<AuditLogEntry> Entries { get; } = [];

        public Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
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

    private sealed class FakePhotoStorageService : IPhotoStorageService
    {
        private int _saveCounter;

        public List<string> SaveFromPathCalls { get; } = [];
        public List<(string OldNumero, string NewNumero)> RenameOficioCalls { get; } = [];
        public List<(string? RelativeFilePath, string? RelativeThumbnailPath)> DeletedEvidenceFilesCalls { get; } = [];

        public Task<StoredPhotoInfo> SaveEvidenceAsync(IFormFile file, string numeroOficio, int consecutivo, CancellationToken cancellationToken = default)
        {
            _saveCounter++;
            return Task.FromResult(new StoredPhotoInfo
            {
                RelativeFilePath = $"stored/file-{_saveCounter}.jpg",
                RelativeThumbnailPath = $"stored/thumb-{_saveCounter}.jpg"
            });
        }

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
            DeletedEvidenceFilesCalls.Add((relativeFilePath, relativeThumbnailPath));
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
            RenameOficioCalls.Add((oldNumeroOficio, newNumeroOficio));
            errorMessage = null;
            return true;
        }

        public bool TryReorderCaseFolders(string numeroOficio, IReadOnlyCollection<(int OldConsecutivo, int NewConsecutivo)> moves, out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        public string RebaseRelativePathForCase(string relativePath, string numeroOficio, int oldConsecutivo, int newConsecutivo)
            => relativePath.Replace($"/{oldConsecutivo:000}/", $"/{newConsecutivo:000}/", StringComparison.Ordinal);

        public string RebaseRelativePathForOficio(string relativePath, string oldNumeroOficio, string newNumeroOficio)
            => relativePath.Replace(oldNumeroOficio, newNumeroOficio, StringComparison.Ordinal);
    }
}
