using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdenToCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Orden",
                table: "Categorias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Inicializar Orden de categorías existentes con ROW_NUMBER por admin
            migrationBuilder.Sql(@"
                WITH OrdenInicial AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY AdministradorId ORDER BY Id) AS NuevoOrden
                    FROM Categorias
                )
                UPDATE c
                SET c.Orden = oi.NuevoOrden
                FROM Categorias c
                INNER JOIN OrdenInicial oi ON c.Id = oi.Id;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Orden",
                table: "Categorias");
        }
    }
}
