using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitacoraEvidencias.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NumeroOficio = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Consecutivo = table.Column<int>(type: "INTEGER", nullable: true),
                    Curp = table.Column<string>(type: "TEXT", maxLength: 18, nullable: true),
                    Cct = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    FolioCertificado = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    EvidenciasCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Oficios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroOficio = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    FechaOficio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Asunto = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Notas = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oficios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosSistema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Usuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    UsuarioNormalizado = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Rol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "INTEGER", nullable: false),
                    BloqueadoHastaUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreadoEnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UltimoAccesoUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PasswordUpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosSistema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CasosCorreccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OficioId = table.Column<int>(type: "INTEGER", nullable: false),
                    Consecutivo = table.Column<int>(type: "INTEGER", nullable: false),
                    Curp = table.Column<string>(type: "TEXT", maxLength: 18, nullable: true),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 140, nullable: true),
                    Cct = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    NivelEducativo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TipoCorreccion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Folio = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    PeriodoInicio = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodoFin = table.Column<int>(type: "INTEGER", nullable: true),
                    FechaVerificacion = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EvidenciaUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", nullable: true),
                    Estatus = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    AuditoriaEstado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AuditoriaUltimoResultado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    AuditoriaUltimaObservacion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AuditoriaUltimoUsuarioId = table.Column<int>(type: "INTEGER", nullable: true),
                    AuditoriaUltimoUsuario = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    AuditoriaUltimaRevisionUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreadoEn = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "EvidenciasFoto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CasoCorreccionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RutaArchivo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RutaMiniatura = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TipoEvidencia = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    FechaEvidencia = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notas = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "IX_AuditLogs_EventType",
                table: "AuditLogs",
                column: "EventType");

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
                name: "IX_CasosCorreccion_Cct",
                table: "CasosCorreccion",
                column: "Cct");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_Curp",
                table: "CasosCorreccion",
                column: "Curp");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_Estatus",
                table: "CasosCorreccion",
                column: "Estatus");

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_Folio",
                table: "CasosCorreccion",
                column: "Folio");

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
                name: "UsuariosSistema");

            migrationBuilder.DropTable(
                name: "CasosCorreccion");

            migrationBuilder.DropTable(
                name: "Oficios");
        }
    }
}
