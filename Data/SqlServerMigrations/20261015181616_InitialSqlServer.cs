using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitacoraEvidencias.Web.Data.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Latin1_General_100_BIN2"),
                    EntityType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true, collation: "Latin1_General_100_BIN2"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, collation: "Latin1_General_100_BIN2"),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true, collation: "Latin1_General_100_BIN2"),
                    NumeroOficio = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Consecutivo = table.Column<int>(type: "int", nullable: true),
                    Curp = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CurpHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CurpSuffix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Cct = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CctHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CctSuffix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true, collation: "Latin1_General_100_BIN2"),
                    FolioCertificado = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true, collation: "Latin1_General_100_BIN2"),
                    FolioCertificadoHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    FolioCertificadoSuffix = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true, collation: "Latin1_General_100_BIN2"),
                    EvidenciasCount = table.Column<int>(type: "int", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Success = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Oficios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroOficio = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_100_BIN2"),
                    FechaOficio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Asunto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Notas = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oficios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosSistema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Usuario = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Latin1_General_100_BIN2"),
                    UsuarioNormalizado = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    PasswordSalt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHastaUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimoAccesoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PasswordUpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosSistema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CasosCorreccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OficioId = table.Column<int>(type: "int", nullable: false),
                    Consecutivo = table.Column<int>(type: "int", nullable: false),
                    Curp = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CurpHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    NombreCompleto = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Cct = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_BIN2"),
                    CctHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Validador = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_BIN2"),
                    AuditorUsuarioId = table.Column<int>(type: "int", nullable: true),
                    NivelEducativo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_BIN2"),
                    TipoCorreccion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Folio = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true, collation: "Latin1_General_100_BIN2"),
                    FolioHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, collation: "Latin1_General_100_BIN2"),
                    PeriodoInicio = table.Column<int>(type: "int", nullable: true),
                    PeriodoFin = table.Column<int>(type: "int", nullable: true),
                    FechaVerificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EvidenciaUrl = table.Column<string>(type: "nvarchar(1600)", maxLength: 1600, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true, collation: "Latin1_General_100_BIN2"),
                    Estatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AuditoriaEstado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_BIN2"),
                    AuditoriaUltimoResultado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, collation: "Latin1_General_100_BIN2"),
                    AuditoriaUltimaObservacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, collation: "Latin1_General_100_BIN2"),
                    AuditoriaUltimoUsuarioId = table.Column<int>(type: "int", nullable: true),
                    AuditoriaUltimoUsuario = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true, collation: "Latin1_General_100_BIN2"),
                    AuditoriaUltimaRevisionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CasosCorreccion", x => x.Id);
                    table.CheckConstraint("CK_CasosCorreccion_AuditoriaEstado", "\"AuditoriaEstado\" IN ('Pendiente', 'Coincidente', 'No coincidente')");
                    table.CheckConstraint("CK_CasosCorreccion_Estatus", "\"Estatus\" IN ('Pendiente', 'En proceso', 'Completado', 'Rechazado')");
                    table.CheckConstraint("CK_CasosCorreccion_NivelEducativo", "\"NivelEducativo\" IN ('Primaria', 'Secundaria')");
                    table.ForeignKey(
                        name: "FK_CasosCorreccion_Oficios_OficioId",
                        column: x => x.OficioId,
                        principalTable: "Oficios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CasosCorreccion_UsuariosSistema_AuditorUsuarioId",
                        column: x => x.AuditorUsuarioId,
                        principalTable: "UsuariosSistema",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvidenciasFoto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CasoCorreccionId = table.Column<int>(type: "int", nullable: false),
                    RutaArchivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RutaMiniatura = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, collation: "Latin1_General_100_BIN2"),
                    TipoEvidencia = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_100_BIN2"),
                    FechaEvidencia = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notas = table.Column<string>(type: "nvarchar(max)", nullable: true, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenciasFoto", x => x.Id);
                    table.CheckConstraint("CK_EvidenciasFoto_TipoEvidencia", "\"TipoEvidencia\" IN ('Oficio', 'Lista', 'Extra')");
                    table.ForeignKey(
                        name: "FK_EvidenciasFoto_CasosCorreccion_CasoCorreccionId",
                        column: x => x.CasoCorreccionId,
                        principalTable: "CasosCorreccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CctHash",
                table: "AuditLogs",
                column: "CctHash");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CurpHash",
                table: "AuditLogs",
                column: "CurpHash");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventType",
                table: "AuditLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_FolioCertificadoHash",
                table: "AuditLogs",
                column: "FolioCertificadoHash");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_NumeroOficio",
                table: "AuditLogs",
                column: "NumeroOficio");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                table: "AuditLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Username",
                table: "AuditLogs",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_AuditoriaEstado",
                table: "CasosCorreccion",
                column: "AuditoriaEstado");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_AuditorUsuarioId",
                table: "CasosCorreccion",
                column: "AuditorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_Cct",
                table: "CasosCorreccion",
                column: "Cct");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_CctHash",
                table: "CasosCorreccion",
                column: "CctHash");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_Curp",
                table: "CasosCorreccion",
                column: "Curp");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_CurpHash",
                table: "CasosCorreccion",
                column: "CurpHash");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_Estatus",
                table: "CasosCorreccion",
                column: "Estatus");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_Folio",
                table: "CasosCorreccion",
                column: "Folio");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_FolioHash",
                table: "CasosCorreccion",
                column: "FolioHash");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_NivelEducativo",
                table: "CasosCorreccion",
                column: "NivelEducativo");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_OficioId_Consecutivo",
                table: "CasosCorreccion",
                columns: new[] { "OficioId", "Consecutivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_OficioId_NivelEducativo",
                table: "CasosCorreccion",
                columns: new[] { "OficioId", "NivelEducativo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasFoto_CasoCorreccionId",
                table: "EvidenciasFoto",
                column: "CasoCorreccionId");

            migrationBuilder.CreateIndex(
                name: "IX_Oficios_NumeroOficio",
                table: "Oficios",
                column: "NumeroOficio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSistema_Rol_Activo",
                table: "UsuariosSistema",
                columns: new[] { "Rol", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSistema_UsuarioNormalizado",
                table: "UsuariosSistema",
                column: "UsuarioNormalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "EvidenciasFoto");

            migrationBuilder.DropTable(
                name: "CasosCorreccion");

            migrationBuilder.DropTable(
                name: "Oficios");

            migrationBuilder.DropTable(
                name: "UsuariosSistema");
        }
    }
}
