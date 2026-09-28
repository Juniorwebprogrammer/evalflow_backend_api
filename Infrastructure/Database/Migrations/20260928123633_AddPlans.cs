using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MaxEmployees = table.Column<int>(type: "integer", nullable: true),
                    MaxActiveCycles = table.Column<int>(type: "integer", nullable: true),
                    MaxCyclesPerYear = table.Column<int>(type: "integer", nullable: true),
                    MaxCustomTemplates = table.Column<int>(type: "integer", nullable: true),
                    MaxDepartments = table.Column<int>(type: "integer", nullable: true),
                    HasAiFeatures = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "Code", "HasAiFeatures", "MaxActiveCycles", "MaxCustomTemplates", "MaxCyclesPerYear", "MaxDepartments", "MaxEmployees", "Nombre" },
                values: new object[,]
                {
                    { 1, "starter", false, 1, 10, 4, 5, 25, "Starter" },
                    { 2, "growth", true, 3, 50, 12, 20, 100, "Growth" },
                    { 3, "enterprise", true, null, null, null, null, null, "Enterprise" }
                });

            // Companies created before plans existed may hold an unknown id (0 or
            // anything the sign-up form sent): move them to Starter so the FK holds.
            migrationBuilder.Sql(@"UPDATE ""Companies"" SET ""PlanId"" = 1 WHERE ""PlanId"" NOT IN (1, 2, 3);");

            // Templates provisioned at sign-up don't count toward the template limit.
            migrationBuilder.Sql(@"UPDATE ""Templates"" SET ""IsDefault"" = TRUE WHERE ""Titulo"" IN (
                'Standard 180° Template (Manager Only)',
                'Comprehensive 360° Template',
                'Self-Assessment Template');");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_PlanId",
                table: "Companies",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Code",
                table: "Plans",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Plans_PlanId",
                table: "Companies",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Plans_PlanId",
                table: "Companies");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_Companies_PlanId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Templates");
        }
    }
}
