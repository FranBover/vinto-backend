using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eat_Experience.Migrations
{
    /// <inheritdoc />
    public partial class Add_AliasTransferencia_TitularCuenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AliasTransferencia",
                table: "Administradores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitularCuenta",
                table: "Administradores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AliasTransferencia",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "TitularCuenta",
                table: "Administradores");
        }
    }
}
