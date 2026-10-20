using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHorarioLocalYCierreManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CerradoManualmente",
                table: "Administradores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ZonaHoraria",
                table: "Administradores",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "America/Argentina/Buenos_Aires");

            migrationBuilder.CreateTable(
                name: "HorariosLocal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdministradorId = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    Modo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Turno1Apertura = table.Column<TimeOnly>(type: "time", nullable: true),
                    Turno1Cierre = table.Column<TimeOnly>(type: "time", nullable: true),
                    Turno2Apertura = table.Column<TimeOnly>(type: "time", nullable: true),
                    Turno2Cierre = table.Column<TimeOnly>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorariosLocal", x => x.Id);
                    table.CheckConstraint("CK_HorariosLocal_DiaSemana", "[DiaSemana] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_HorariosLocal_Modo", "[Modo] IN (N'Cerrado', N'24h', N'Turnos')");
                    table.ForeignKey(
                        name: "FK_HorariosLocal_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HorariosLocal_AdministradorId_DiaSemana",
                table: "HorariosLocal",
                columns: new[] { "AdministradorId", "DiaSemana" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HorariosLocal");

            migrationBuilder.DropColumn(
                name: "CerradoManualmente",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "ZonaHoraria",
                table: "Administradores");
        }
    }
}
