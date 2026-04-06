using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eat_Experience.Migrations
{
    /// <inheritdoc />
    public partial class AddHorariosUbicacionUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Horarios",
                table: "Administradores",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UbicacionUrl",
                table: "Administradores",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Horarios",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "UbicacionUrl",
                table: "Administradores");
        }
    }
}
