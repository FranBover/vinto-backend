using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class Add_DetallePedidoExtra_BienHecho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateTable(
                name: "DetallePedidoExtras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DetallePedidoId = table.Column<int>(type: "int", nullable: false),
                    ProductoExtraId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallePedidoExtras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallePedidoExtras_DetallesPedido_DetallePedidoId",
                        column: x => x.DetallePedidoId,
                        principalTable: "DetallesPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallePedidoExtras_ProductoExtras_ProductoExtraId",
                        column: x => x.ProductoExtraId,
                        principalTable: "ProductoExtras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedidoExtras_DetallePedidoId",
                table: "DetallePedidoExtras",
                column: "DetallePedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedidoExtras_ProductoExtraId",
                table: "DetallePedidoExtras",
                column: "ProductoExtraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetallePedidoExtras");

            migrationBuilder.AddColumn<int>(
                name: "DetallePedidoId",
                table: "ProductoExtras",
                type: "int",
                nullable: true);

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
    }
}
