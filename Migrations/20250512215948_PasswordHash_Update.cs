using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eat_Experience.Migrations
{
    /// <inheritdoc />
    public partial class PasswordHash_Update : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Contraseña",
                table: "Administradores",
                newName: "PasswordHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "Administradores",
                newName: "Contraseña");
        }
    }
}
