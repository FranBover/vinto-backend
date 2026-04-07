using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class update_pedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormaEntrega",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FormaPago",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MontoPagoEfectivo",
                table: "Pedidos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Pedidos",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormaEntrega",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "FormaPago",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MontoPagoEfectivo",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Pedidos");
        }
    }
}
