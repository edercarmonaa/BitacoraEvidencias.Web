using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitacoraEvidencias.Web.Data.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class PagingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UsuariosSistema_Usuario_Id",
                table: "UsuariosSistema",
                columns: new[] { "Usuario", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Oficios_FechaOficio_NumeroOficio_Id",
                table: "Oficios",
                columns: new[] { "FechaOficio", "NumeroOficio", "Id" },
                descending: new[] { true, false, false })
                .Annotation("SqlServer:Include", new[] { "Asunto" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenciasFoto_CasoCorreccionId_FechaEvidencia_Id",
                table: "EvidenciasFoto",
                columns: new[] { "CasoCorreccionId", "FechaEvidencia", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_CreadoEn_Id",
                table: "CasosCorreccion",
                columns: new[] { "CreadoEn", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_Estatus_CreadoEn_Id",
                table: "CasosCorreccion",
                columns: new[] { "Estatus", "CreadoEn", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_FechaVerificacion_Id",
                table: "CasosCorreccion",
                columns: new[] { "FechaVerificacion", "Id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_OficioId_AuditoriaEstado",
                table: "CasosCorreccion",
                columns: new[] { "OficioId", "AuditoriaEstado" })
                .Annotation("SqlServer:Include", new[] { "EvidenciaUrl", "Consecutivo" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc_Id",
                table: "AuditLogs",
                columns: new[] { "OccurredAtUtc", "Id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UsuariosSistema_Usuario_Id",
                table: "UsuariosSistema");

            migrationBuilder.DropIndex(
                name: "IX_Oficios_FechaOficio_NumeroOficio_Id",
                table: "Oficios");

            migrationBuilder.DropIndex(
                name: "IX_EvidenciasFoto_CasoCorreccionId_FechaEvidencia_Id",
                table: "EvidenciasFoto");

            migrationBuilder.DropIndex(
                name: "IX_CasosCorreccion_CreadoEn_Id",
                table: "CasosCorreccion");

            migrationBuilder.DropIndex(
                name: "IX_CasosCorreccion_Estatus_CreadoEn_Id",
                table: "CasosCorreccion");

            migrationBuilder.DropIndex(
                name: "IX_CasosCorreccion_FechaVerificacion_Id",
                table: "CasosCorreccion");

            migrationBuilder.DropIndex(
                name: "IX_CasosCorreccion_OficioId_AuditoriaEstado",
                table: "CasosCorreccion");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_OccurredAtUtc_Id",
                table: "AuditLogs");
        }
    }
}
