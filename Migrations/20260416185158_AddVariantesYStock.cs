using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantesYStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Stock",
                table: "Productos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TieneVariantes",
                table: "Productos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VarianteProductoId",
                table: "DetallesPedido",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoDeshabilitarSinStock",
                table: "Administradores",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StockBajoAlerta",
                table: "Administradores",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TiposVariante",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposVariante", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TiposVariante_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpcionesVariante",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoVarianteId = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpcionesVariante", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpcionesVariante_TiposVariante_TipoVarianteId",
                        column: x => x.TipoVarianteId,
                        principalTable: "TiposVariante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VariantesProducto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    Opcion1Id = table.Column<int>(type: "int", nullable: false),
                    Opcion2Id = table.Column<int>(type: "int", nullable: true),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: true),
                    Disponible = table.Column<bool>(type: "bit", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantesProducto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariantesProducto_OpcionesVariante_Opcion1Id",
                        column: x => x.Opcion1Id,
                        principalTable: "OpcionesVariante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantesProducto_OpcionesVariante_Opcion2Id",
                        column: x => x.Opcion2Id,
                        principalTable: "OpcionesVariante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantesProducto_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdministradorId = table.Column<int>(type: "int", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    VarianteProductoId = table.Column<int>(type: "int", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    StockAnterior = table.Column<int>(type: "int", nullable: false),
                    StockNuevo = table.Column<int>(type: "int", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_VariantesProducto_VarianteProductoId",
                        column: x => x.VarianteProductoId,
                        principalTable: "VariantesProducto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_VarianteProductoId",
                table: "DetallesPedido",
                column: "VarianteProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_AdministradorId",
                table: "MovimientosStock",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_ProductoId",
                table: "MovimientosStock",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_VarianteProductoId",
                table: "MovimientosStock",
                column: "VarianteProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_OpcionesVariante_TipoVarianteId",
                table: "OpcionesVariante",
                column: "TipoVarianteId");

            migrationBuilder.CreateIndex(
                name: "IX_TiposVariante_ProductoId",
                table: "TiposVariante",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantesProducto_Opcion1Id",
                table: "VariantesProducto",
                column: "Opcion1Id");

            migrationBuilder.CreateIndex(
                name: "IX_VariantesProducto_Opcion2Id",
                table: "VariantesProducto",
                column: "Opcion2Id");

            migrationBuilder.CreateIndex(
                name: "IX_VariantesProducto_ProductoId",
                table: "VariantesProducto",
                column: "ProductoId");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesPedido_VariantesProducto_VarianteProductoId",
                table: "DetallesPedido",
                column: "VarianteProductoId",
                principalTable: "VariantesProducto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesPedido_VariantesProducto_VarianteProductoId",
                table: "DetallesPedido");

            migrationBuilder.DropTable(
                name: "MovimientosStock");

            migrationBuilder.DropTable(
                name: "VariantesProducto");

            migrationBuilder.DropTable(
                name: "OpcionesVariante");

            migrationBuilder.DropTable(
                name: "TiposVariante");

            migrationBuilder.DropIndex(
                name: "IX_DetallesPedido_VarianteProductoId",
                table: "DetallesPedido");

            migrationBuilder.DropColumn(
                name: "Stock",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "TieneVariantes",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "VarianteProductoId",
                table: "DetallesPedido");

            migrationBuilder.DropColumn(
                name: "AutoDeshabilitarSinStock",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "StockBajoAlerta",
                table: "Administradores");
        }
    }
}
