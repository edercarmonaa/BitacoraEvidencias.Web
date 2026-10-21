using BitacoraEvidencias.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BitacoraEvidencias.Web.Data.SqlServerMigrations;

[DbContext(typeof(SqlServerAppDbContext))]
[Migration("20261020120000_ReportSummaries")]
public sealed partial class ReportSummaries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE dbo.ReportChangeQueue (
                Id bigint IDENTITY PRIMARY KEY, OficioId int NOT NULL);
            CREATE INDEX IX_ReportChangeQueue_OficioId ON dbo.ReportChangeQueue(OficioId, Id);
            CREATE TABLE dbo.ReportRefreshState (
                Id int NOT NULL PRIMARY KEY CHECK (Id=1), Ready bit NOT NULL, UpdatedAtUtc datetime2 NULL);
            INSERT dbo.ReportRefreshState VALUES (1,0,NULL);
            CREATE TABLE dbo.ReportOffices (
                Id int NOT NULL PRIMARY KEY, FechaOficio datetime2 NOT NULL,
                PrimeraFechaVerificacion datetime2 NULL, Correcto bit NOT NULL, Incorrecto bit NOT NULL);
            CREATE INDEX IX_ReportOffices_Verificacion ON dbo.ReportOffices(PrimeraFechaVerificacion) INCLUDE(FechaOficio);
            CREATE TABLE dbo.ReportCases (
                Id int NOT NULL PRIMARY KEY, OficioId int NOT NULL,
                NumeroOficio nvarchar(80) COLLATE Latin1_General_100_BIN2 NOT NULL,
                Consecutivo int NOT NULL, CreadoEn datetime2 NOT NULL, FechaVerificacion datetime2 NULL,
                NivelEducativo nvarchar(20) COLLATE Latin1_General_100_BIN2 NOT NULL,
                TipoCorreccion nvarchar(80) COLLATE Latin1_General_100_BIN2 NOT NULL,
                Estatus nvarchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
                Fotos int NOT NULL, TieneUrl bit NOT NULL, Incorrecto bit NOT NULL);
            CREATE INDEX IX_ReportCases_OficioId ON dbo.ReportCases(OficioId);
            CREATE INDEX IX_ReportCases_Verificacion ON dbo.ReportCases(FechaVerificacion)
                INCLUDE(OficioId, NivelEducativo, TipoCorreccion, Estatus, Fotos, TieneUrl);
            INSERT dbo.ReportChangeQueue(OficioId) SELECT Id FROM dbo.Oficios;
            """);

        // Append-only notifications share the source transaction. No unique-key upsert
        // or full report calculation runs inside a user write. IDs survive deletions.
        migrationBuilder.Sql("""
            CREATE TRIGGER dbo.TR_Oficios_ReportChanges ON dbo.Oficios AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                INSERT dbo.ReportChangeQueue(OficioId) SELECT Id FROM inserted UNION SELECT Id FROM deleted;
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER dbo.TR_Casos_ReportChanges ON dbo.CasosCorreccion AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                INSERT dbo.ReportChangeQueue(OficioId) SELECT OficioId FROM inserted UNION SELECT OficioId FROM deleted;
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER dbo.TR_Evidencias_ReportChanges ON dbo.EvidenciasFoto AFTER INSERT, UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                INSERT dbo.ReportChangeQueue(OficioId)
                SELECT DISTINCT c.OficioId FROM dbo.CasosCorreccion c
                JOIN (SELECT CasoCorreccionId FROM inserted UNION SELECT CasoCorreccionId FROM deleted) e
                    ON e.CasoCorreccionId=c.Id;
                -- Cascade deletes are also covered by the case/office triggers.
            END
            """);
        migrationBuilder.Sql("""
            CREATE PROCEDURE dbo.RefreshReportSummaries @BatchSize int=25 AS
            BEGIN
                SET NOCOUNT ON;
                SET XACT_ABORT ON;
                SET LOCK_TIMEOUT 1000;
                IF @BatchSize < 1 OR @BatchSize > 100 THROW 50001,'Invalid report batch size.',1;
                SET DEADLOCK_PRIORITY LOW;
                BEGIN TRY
                    BEGIN TRANSACTION;
                    DECLARE @lock int;
                    EXEC @lock=sys.sp_getapplock @Resource=N'BitacoraReportSummaries',
                        @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;
                    IF @lock < 0
                    BEGIN ROLLBACK; SET LOCK_TIMEOUT -1; SET DEADLOCK_PRIORITY NORMAL; RETURN; END;
                    -- Capture exact event watermarks BEFORE taking serializable locks.
                    -- Events committed later are never removed by this batch.
                    DECLARE @work TABLE (OficioId int PRIMARY KEY, LastId bigint);
                    INSERT @work SELECT TOP (@BatchSize) OficioId,MAX(Id)
                        FROM dbo.ReportChangeQueue GROUP BY OficioId ORDER BY MIN(Id);
                    IF EXISTS(SELECT 1 FROM @work) OR EXISTS(SELECT 1 FROM dbo.ReportRefreshState WHERE Ready=0)
                    BEGIN
                        SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
                        DELETE r FROM dbo.ReportCases r JOIN @work w ON w.OficioId=r.OficioId;
                        DELETE r FROM dbo.ReportOffices r JOIN @work w ON w.OficioId=r.Id;
                        INSERT dbo.ReportCases
                        SELECT c.Id,c.OficioId,o.NumeroOficio,c.Consecutivo,c.CreadoEn,c.FechaVerificacion,
                            c.NivelEducativo,c.TipoCorreccion,c.Estatus,
                            (SELECT COUNT(*) FROM dbo.EvidenciasFoto e WHERE e.CasoCorreccionId=c.Id),
                            CASE WHEN c.EvidenciaUrl IS NOT NULL AND c.EvidenciaUrl<>N'' THEN 1 ELSE 0 END,
                            CASE WHEN c.AuditoriaEstado COLLATE Latin1_General_100_BIN2=N'No coincidente'
                                AND (EXISTS(SELECT 1 FROM dbo.EvidenciasFoto e WHERE e.CasoCorreccionId=c.Id)
                                    OR (c.EvidenciaUrl IS NOT NULL AND c.EvidenciaUrl<>N'')) THEN 1 ELSE 0 END
                        FROM dbo.CasosCorreccion c JOIN @work w ON w.OficioId=c.OficioId
                        JOIN dbo.Oficios o ON o.Id=c.OficioId;
                        INSERT dbo.ReportOffices
                        SELECT o.Id,o.FechaOficio,MIN(c.FechaVerificacion),
                            CASE WHEN COUNT(c.Id)>0 AND
                                SUM(CASE WHEN r.Fotos>0 OR r.TieneUrl=1 THEN 1 ELSE 0 END)=COUNT(c.Id) AND
                                SUM(CASE WHEN TRIM(c.AuditoriaEstado) COLLATE Latin1_General_100_BIN2=N'Coincidente' THEN 1 ELSE 0 END)=COUNT(c.Id)
                                THEN 1 ELSE 0 END,
                            CASE WHEN COUNT(c.Id)>0 AND
                                SUM(CASE WHEN r.Fotos>0 OR r.TieneUrl=1 THEN 1 ELSE 0 END)=COUNT(c.Id) AND
                                SUM(CASE WHEN TRIM(c.AuditoriaEstado) COLLATE Latin1_General_100_BIN2 IN (N'Coincidente',N'No coincidente') THEN 1 ELSE 0 END)=COUNT(c.Id) AND
                                SUM(CASE WHEN TRIM(c.AuditoriaEstado) COLLATE Latin1_General_100_BIN2=N'No coincidente' THEN 1 ELSE 0 END)>0
                                THEN 1 ELSE 0 END
                        FROM dbo.Oficios o JOIN @work w ON w.OficioId=o.Id
                        LEFT JOIN dbo.CasosCorreccion c ON c.OficioId=o.Id
                        LEFT JOIN dbo.ReportCases r ON r.Id=c.Id
                        GROUP BY o.Id,o.FechaOficio;
                        DELETE q FROM dbo.ReportChangeQueue q JOIN @work w ON w.OficioId=q.OficioId AND q.Id<=w.LastId;
                        UPDATE dbo.ReportRefreshState SET
                            Ready=CASE WHEN NOT EXISTS(SELECT 1 FROM dbo.ReportChangeQueue) THEN 1 ELSE Ready END,
                            UpdatedAtUtc=CASE WHEN NOT EXISTS(SELECT 1 FROM dbo.ReportChangeQueue)
                                THEN SYSUTCDATETIME() ELSE UpdatedAtUtc END WHERE Id=1;
                    END;
                    COMMIT;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET LOCK_TIMEOUT -1;
                    SET DEADLOCK_PRIORITY NORMAL;
                END TRY
                BEGIN CATCH
                    IF @@TRANCOUNT>0 ROLLBACK;
                    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
                    SET LOCK_TIMEOUT -1;
                    SET DEADLOCK_PRIORITY NORMAL;
                    THROW;
                END CATCH
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP PROCEDURE dbo.RefreshReportSummaries;
            DROP TRIGGER dbo.TR_Evidencias_ReportChanges;
            DROP TRIGGER dbo.TR_Casos_ReportChanges;
            DROP TRIGGER dbo.TR_Oficios_ReportChanges;
            DROP TABLE dbo.ReportCases, dbo.ReportOffices, dbo.ReportRefreshState, dbo.ReportChangeQueue;
            """);
    }
}
