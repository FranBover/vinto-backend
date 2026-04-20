using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class Fase3_DescuentosYCupones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoCupon",
                table: "Pedidos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CuponId",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoDescuentoCupon",
                table: "Pedidos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MontoDescuentoProductos",
                table: "Pedidos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalSinDescuentos",
                table: "Pedidos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Cupones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdministradorId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaVencimiento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LimiteUsos = table.Column<int>(type: "int", nullable: true),
                    UsosActuales = table.Column<int>(type: "int", nullable: false),
                    PedidoMinimo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cupones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cupones_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Descuentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdministradorId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: true),
                    CategoriaId = table.Column<int>(type: "int", nullable: true),
                    AplicaAPedidoCompleto = table.Column<bool>(type: "bit", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Descuentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Descuentos_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Descuentos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Descuentos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PreviewsActualizacionPrecios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Redondear = table.Column<bool>(type: "bit", nullable: false),
                    TotalAfectados = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    FechaExpiracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aplicado = table.Column<bool>(type: "bit", nullable: false),
                    FechaAplicacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreviewsActualizacionPrecios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreviewsActualizacionPrecios_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreviewsActualizacionPrecios_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsosCupones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CuponId = table.Column<int>(type: "int", nullable: false),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    MontoDescontado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaUso = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Liberado = table.Column<bool>(type: "bit", nullable: false),
                    FechaLiberacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsosCupones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsosCupones_Cupones_CuponId",
                        column: x => x.CuponId,
                        principalTable: "Cupones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsosCupones_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetallePedidoDescuentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    DetallePedidoId = table.Column<int>(type: "int", nullable: true),
                    DescuentoId = table.Column<int>(type: "int", nullable: true),
                    NombreDescuentoSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoDescuento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MontoDescontado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallePedidoDescuentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallePedidoDescuentos_Descuentos_DescuentoId",
                        column: x => x.DescuentoId,
                        principalTable: "Descuentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DetallePedidoDescuentos_DetallesPedido_DetallePedidoId",
                        column: x => x.DetallePedidoId,
                        principalTable: "DetallesPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallePedidoDescuentos_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreviewActualizacionPreciosItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductoId = table.Column<int>(type: "int", nullable: false),
                    PrecioActual = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PrecioNuevo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreviewActualizacionPreciosItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreviewActualizacionPreciosItems_PreviewsActualizacionPrecios_PreviewId",
                        column: x => x.PreviewId,
                        principalTable: "PreviewsActualizacionPrecios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PreviewActualizacionPreciosItems_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_CuponId",
                table: "Pedidos",
                column: "CuponId");

            migrationBuilder.CreateIndex(
                name: "IX_Cupones_AdministradorId_Codigo",
                table: "Cupones",
                columns: new[] { "AdministradorId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Descuentos_AdministradorId_Activo",
                table: "Descuentos",
                columns: new[] { "AdministradorId", "Activo" });

            migrationBuilder.CreateIndex(
                name: "IX_Descuentos_CategoriaId",
                table: "Descuentos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Descuentos_ProductoId",
                table: "Descuentos",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedidoDescuentos_DescuentoId",
                table: "DetallePedidoDescuentos",
                column: "DescuentoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedidoDescuentos_DetallePedidoId",
                table: "DetallePedidoDescuentos",
                column: "DetallePedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedidoDescuentos_PedidoId",
                table: "DetallePedidoDescuentos",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_PreviewActualizacionPreciosItems_PreviewId",
                table: "PreviewActualizacionPreciosItems",
                column: "PreviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PreviewActualizacionPreciosItems_ProductoId",
                table: "PreviewActualizacionPreciosItems",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_PreviewsActualizacionPrecios_AdministradorId_Aplicado",
                table: "PreviewsActualizacionPrecios",
                columns: new[] { "AdministradorId", "Aplicado" });

            migrationBuilder.CreateIndex(
                name: "IX_PreviewsActualizacionPrecios_CategoriaId",
                table: "PreviewsActualizacionPrecios",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsosCupones_CuponId",
                table: "UsosCupones",
                column: "CuponId");

            migrationBuilder.CreateIndex(
                name: "IX_UsosCupones_PedidoId",
                table: "UsosCupones",
                column: "PedidoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Cupones_CuponId",
                table: "Pedidos",
                column: "CuponId",
                principalTable: "Cupones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Cupones_CuponId",
                table: "Pedidos");

            migrationBuilder.DropTable(
                name: "DetallePedidoDescuentos");

            migrationBuilder.DropTable(
                name: "PreviewActualizacionPreciosItems");

            migrationBuilder.DropTable(
                name: "UsosCupones");

            migrationBuilder.DropTable(
                name: "Descuentos");

            migrationBuilder.DropTable(
                name: "PreviewsActualizacionPrecios");

            migrationBuilder.DropTable(
                name: "Cupones");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_CuponId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "CodigoCupon",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "CuponId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MontoDescuentoCupon",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MontoDescuentoProductos",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "SubtotalSinDescuentos",
                table: "Pedidos");
        }
    }
}
