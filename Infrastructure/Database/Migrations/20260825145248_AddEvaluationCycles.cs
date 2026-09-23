using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvaluationCycles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmpresaID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationCycles_Companies_EmpresaID",
                        column: x => x.EmpresaID,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationCycleTemplate",
                columns: table => new
                {
                    CiclosId = table.Column<int>(type: "integer", nullable: false),
                    TemplatesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCycleTemplate", x => new { x.CiclosId, x.TemplatesId });
                    table.ForeignKey(
                        name: "FK_EvaluationCycleTemplate_EvaluationCycles_CiclosId",
                        column: x => x.CiclosId,
                        principalTable: "EvaluationCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationCycleTemplate_Templates_TemplatesId",
                        column: x => x.TemplatesId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCycles_EmpresaID",
                table: "EvaluationCycles",
                column: "EmpresaID");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCycleTemplate_TemplatesId",
                table: "EvaluationCycleTemplate",
                column: "TemplatesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationCycleTemplate");

            migrationBuilder.DropTable(
                name: "EvaluationCycles");
        }
    }
}
