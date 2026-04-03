using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eat_Experience.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidosYDetalles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DetallePedidoId",
                table: "ProductoExtras",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NombreCliente",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "DireccionCliente",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelefonoCliente",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioUnitario",
                table: "DetallesPedido",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoExtras_DetallePedidoId",
                table: "ProductoExtras",
                column: "DetallePedidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductoExtras_DetallesPedido_DetallePedidoId",
                table: "ProductoExtras",
                column: "DetallePedidoId",
                principalTable: "DetallesPedido",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductoExtras_DetallesPedido_DetallePedidoId",
                table: "ProductoExtras");

            migrationBuilder.DropIndex(
                name: "IX_ProductoExtras_DetallePedidoId",
                table: "ProductoExtras");

            migrationBuilder.DropColumn(
                name: "DetallePedidoId",
                table: "ProductoExtras");

            migrationBuilder.DropColumn(
                name: "TelefonoCliente",
                table: "Pedidos");

            migrationBuilder.AlterColumn<string>(
                name: "NombreCliente",
                table: "Pedidos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "Pedidos",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DireccionCliente",
                table: "Pedidos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioUnitario",
                table: "DetallesPedido",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }
    }
}
