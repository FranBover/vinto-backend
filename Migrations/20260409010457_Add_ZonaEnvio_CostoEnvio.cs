using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class Add_ZonaEnvio_CostoEnvio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostoEnvio",
                table: "Administradores",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZonaEnvio",
                table: "Administradores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostoEnvio",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "ZonaEnvio",
                table: "Administradores");
        }
    }
}
