using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoFieldsToPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoCollectionId",
                table: "Pedidos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MercadoPagoFechaPago",
                table: "Pedidos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPaymentId",
                table: "Pedidos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPreferenceId",
                table: "Pedidos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoStatus",
                table: "Pedidos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoStatusDetail",
                table: "Pedidos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MercadoPagoCollectionId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MercadoPagoFechaPago",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MercadoPagoPaymentId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MercadoPagoPreferenceId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MercadoPagoStatus",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MercadoPagoStatusDetail",
                table: "Pedidos");
        }
    }
}
