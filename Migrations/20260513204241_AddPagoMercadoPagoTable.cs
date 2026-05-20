using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoMercadoPagoTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PagosMercadoPago",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StatusDetail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaEvento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawWebhookData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcesadoConExito = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosMercadoPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosMercadoPago_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PagosMercadoPago_PaymentId_Status",
                table: "PagosMercadoPago",
                columns: new[] { "PaymentId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagosMercadoPago_PedidoId",
                table: "PagosMercadoPago",
                column: "PedidoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagosMercadoPago");
        }
    }
}
