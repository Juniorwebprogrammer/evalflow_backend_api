using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddJobPositionsAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CargoId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    EmpresaID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobPositions_Companies_EmpresaID",
                        column: x => x.EmpresaID,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CargoId",
                table: "Users",
                column: "CargoId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPositions_EmpresaID",
                table: "JobPositions",
                column: "EmpresaID");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_JobPositions_CargoId",
                table: "Users",
                column: "CargoId",
                principalTable: "JobPositions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_JobPositions_CargoId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "JobPositions");

            migrationBuilder.DropIndex(
                name: "IX_Users_CargoId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CargoId",
                table: "Users");
        }
    }
}
