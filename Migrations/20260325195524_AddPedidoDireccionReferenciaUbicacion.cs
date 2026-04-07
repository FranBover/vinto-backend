using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidoDireccionReferenciaUbicacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenciaDireccion",
                table: "Pedidos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UbicacionUrl",
                table: "Pedidos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenciaDireccion",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "UbicacionUrl",
                table: "Pedidos");
        }
    }
}
