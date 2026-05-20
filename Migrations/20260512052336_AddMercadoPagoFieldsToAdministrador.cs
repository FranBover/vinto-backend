using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoFieldsToAdministrador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoAccessToken",
                table: "Administradores",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MercadoPagoConectado",
                table: "Administradores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPublicKey",
                table: "Administradores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoRefreshToken",
                table: "Administradores",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MercadoPagoTokenExpiresAt",
                table: "Administradores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoUserId",
                table: "Administradores",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MercadoPagoAccessToken",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "MercadoPagoConectado",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "MercadoPagoPublicKey",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "MercadoPagoRefreshToken",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "MercadoPagoTokenExpiresAt",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "MercadoPagoUserId",
                table: "Administradores");
        }
    }
}
