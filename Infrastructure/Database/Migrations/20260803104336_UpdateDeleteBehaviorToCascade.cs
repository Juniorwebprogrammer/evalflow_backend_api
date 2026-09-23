using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDeleteBehaviorToCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Companies_EmpresaID",
                table: "Users");

           migrationBuilder.AddForeignKey(
                name: "FK_Users_Companies_EmpresaID",
                table: "Users",
                column: "EmpresaID",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Companies_EmpresaID",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Cif",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "DireccionFiscal",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Sector",
                table: "Companies");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Companies_EmpresaID",
                table: "Users",
                column: "EmpresaID",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
