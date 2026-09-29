using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitacoraEvidencias.Web.Data.Migrations
{
    public partial class AddCaseValidatorAndAuditor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuditorUsuarioId",
                table: "CasosCorreccion",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Validador",
                table: "CasosCorreccion",
                type: "TEXT",
                maxLength: 140,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CasosCorreccion_AuditorUsuarioId",
                table: "CasosCorreccion",
                column: "AuditorUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_CasosCorreccion_UsuariosSistema_AuditorUsuarioId",
                table: "CasosCorreccion",
                column: "AuditorUsuarioId",
                principalTable: "UsuariosSistema",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CasosCorreccion_UsuariosSistema_AuditorUsuarioId",
                table: "CasosCorreccion");

            migrationBuilder.DropIndex(
                name: "IX_CasosCorreccion_AuditorUsuarioId",
                table: "CasosCorreccion");

            migrationBuilder.DropColumn(
                name: "AuditorUsuarioId",
                table: "CasosCorreccion");

            migrationBuilder.DropColumn(
                name: "Validador",
                table: "CasosCorreccion");
        }
    }
}
