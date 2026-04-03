using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eat_Experience.Migrations
{
    /// <inheritdoc />
    public partial class multitenantv1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallePedidoExtras_DetallesPedido_DetallePedidoId",
                table: "DetallePedidoExtras");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesPedido_Productos_ProductoId",
                table: "DetallesPedido");

            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Administradores_AdministradorId",
                table: "Pedidos");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductoExtras_Productos_ProductoId",
                table: "ProductoExtras");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Categorias_CategoriaId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_AdministradorId",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_DetallePedidoExtras_DetallePedidoId",
                table: "DetallePedidoExtras");

            migrationBuilder.AlterColumn<decimal>(
                name: "Precio",
                table: "Productos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Productos",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "AdministradorId",
                table: "Productos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioAdicional",
                table: "ProductoExtras",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Pedidos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.AlterColumn<string>(
                name: "NombreCliente",
                table: "Pedidos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FormaPago",
                table: "Pedidos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FormaEntrega",
                table: "Pedidos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Fecha",
                table: "Pedidos",
                type: "datetime2",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "Pedidos",
                type: "nvarchar(30)",
                maxLength: 30,
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

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Categorias",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "AdministradorId",
                table: "Categorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaRegistro",
                table: "Administradores",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.CreateIndex(
                name: "IX_Productos_AdministradorId_Nombre",
                table: "Productos",
                columns: new[] { "AdministradorId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_AdministradorId_Fecha",
                table: "Pedidos",
                columns: new[] { "AdministradorId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedidoExtras_DetallePedidoId_ProductoExtraId",
                table: "DetallePedidoExtras",
                columns: new[] { "DetallePedidoId", "ProductoExtraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categorias_AdministradorId_Nombre",
                table: "Categorias",
                columns: new[] { "AdministradorId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Administradores_Email",
                table: "Administradores",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categorias_Administradores_AdministradorId",
                table: "Categorias",
                column: "AdministradorId",
                principalTable: "Administradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallePedidoExtras_DetallesPedido_DetallePedidoId",
                table: "DetallePedidoExtras",
                column: "DetallePedidoId",
                principalTable: "DetallesPedido",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesPedido_Productos_ProductoId",
                table: "DetallesPedido",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Administradores_AdministradorId",
                table: "Pedidos",
                column: "AdministradorId",
                principalTable: "Administradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductoExtras_Productos_ProductoId",
                table: "ProductoExtras",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Administradores_AdministradorId",
                table: "Productos",
                column: "AdministradorId",
                principalTable: "Administradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Categorias_CategoriaId",
                table: "Productos",
                column: "CategoriaId",
                principalTable: "Categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categorias_Administradores_AdministradorId",
                table: "Categorias");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallePedidoExtras_DetallesPedido_DetallePedidoId",
                table: "DetallePedidoExtras");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesPedido_Productos_ProductoId",
                table: "DetallesPedido");

            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Administradores_AdministradorId",
                table: "Pedidos");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductoExtras_Productos_ProductoId",
                table: "ProductoExtras");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Administradores_AdministradorId",
                table: "Productos");

            migrationBuilder.DropForeignKey(
                name: "FK_Productos_Categorias_CategoriaId",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Productos_AdministradorId_Nombre",
                table: "Productos");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_AdministradorId_Fecha",
                table: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_DetallePedidoExtras_DetallePedidoId_ProductoExtraId",
                table: "DetallePedidoExtras");

            migrationBuilder.DropIndex(
                name: "IX_Categorias_AdministradorId_Nombre",
                table: "Categorias");

            migrationBuilder.DropIndex(
                name: "IX_Administradores_Email",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "AdministradorId",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "AdministradorId",
                table: "Categorias");

            migrationBuilder.AlterColumn<decimal>(
                name: "Precio",
                table: "Productos",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Descripcion",
                table: "Productos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PrecioAdicional",
                table: "ProductoExtras",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Pedidos",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "NombreCliente",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "FormaPago",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "FormaEntrega",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Fecha",
                table: "Pedidos",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldMaxLength: 30,
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "Estado",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "DireccionCliente",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Categorias",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "FechaRegistro",
                table: "Administradores",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_AdministradorId",
                table: "Pedidos",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedidoExtras_DetallePedidoId",
                table: "DetallePedidoExtras",
                column: "DetallePedidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallePedidoExtras_DetallesPedido_DetallePedidoId",
                table: "DetallePedidoExtras",
                column: "DetallePedidoId",
                principalTable: "DetallesPedido",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesPedido_Productos_ProductoId",
                table: "DetallesPedido",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Administradores_AdministradorId",
                table: "Pedidos",
                column: "AdministradorId",
                principalTable: "Administradores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductoExtras_Productos_ProductoId",
                table: "ProductoExtras",
                column: "ProductoId",
                principalTable: "Productos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Productos_Categorias_CategoriaId",
                table: "Productos",
                column: "CategoriaId",
                principalTable: "Categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
