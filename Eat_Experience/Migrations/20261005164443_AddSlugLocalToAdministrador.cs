using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugLocalToAdministrador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Se agrega nullable, se pueblan los registros existentes con valores FIJOS (para no
            // cambiar las URLs públicas actuales) y recién ahí se pasa a NOT NULL. Con el default ""
            // que genera EF por defecto, el índice único fallaría con más de una fila.
            migrationBuilder.AddColumn<string>(
                name: "SlugLocal",
                table: "Administradores",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.Sql("EXEC(N'UPDATE Administradores SET SlugLocal = N''admin-prueba'' WHERE Id = 1;');");
            migrationBuilder.Sql("EXEC(N'UPDATE Administradores SET SlugLocal = N''pati-a-la-parri'' WHERE Id = 2;');");
            migrationBuilder.Sql("EXEC(N'UPDATE Administradores SET SlugLocal = N''carripollo'' WHERE Id = 3;');");
            migrationBuilder.Sql("EXEC(N'UPDATE Administradores SET SlugLocal = N''ejemplo'' WHERE Id = 4;');");

            // Los UPDATE van en EXEC para que el script SQL compile aunque la columna se cree en el mismo lote.
            // Falla (y revierte la migración) si queda algún administrador sin slug.
            migrationBuilder.AlterColumn<string>(
                name: "SlugLocal",
                table: "Administradores",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Administradores_SlugLocal",
                table: "Administradores",
                column: "SlugLocal",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Administradores_SlugLocal",
                table: "Administradores");

            migrationBuilder.DropColumn(
                name: "SlugLocal",
                table: "Administradores");
        }
    }
}
