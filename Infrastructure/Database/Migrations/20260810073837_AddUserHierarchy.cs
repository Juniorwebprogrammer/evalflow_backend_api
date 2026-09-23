using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Department_Companies_EmpresaID",
                table: "Department");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Department_DepartamentoId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Department",
                table: "Department");

            migrationBuilder.RenameTable(
                name: "Department",
                newName: "Departments");

            migrationBuilder.RenameIndex(
                name: "IX_Department_EmpresaID",
                table: "Departments",
                newName: "IX_Departments_EmpresaID");

            migrationBuilder.AddColumn<int>(
                name: "SuperiorId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Departments",
                table: "Departments",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_SuperiorId",
                table: "Users",
                column: "SuperiorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Companies_EmpresaID",
                table: "Departments",
                column: "EmpresaID",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartamentoId",
                table: "Users",
                column: "DepartamentoId",
                principalTable: "Departments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_SuperiorId",
                table: "Users",
                column: "SuperiorId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Companies_EmpresaID",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartamentoId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_SuperiorId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_SuperiorId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Departments",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "SuperiorId",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "Departments",
                newName: "Department");

            migrationBuilder.RenameIndex(
                name: "IX_Departments_EmpresaID",
                table: "Department",
                newName: "IX_Department_EmpresaID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Department",
                table: "Department",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Department_Companies_EmpresaID",
                table: "Department",
                column: "EmpresaID",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Department_DepartamentoId",
                table: "Users",
                column: "DepartamentoId",
                principalTable: "Department",
                principalColumn: "Id");
        }
    }
}
