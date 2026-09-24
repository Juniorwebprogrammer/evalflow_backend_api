using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationResultsAndAcceptances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCompletado",
                table: "EvaluationCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscrepancyAcceptances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvaluationCycleId = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    EvaluatedUserId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    AcceptedSource = table.Column<int>(type: "integer", nullable: false),
                    AcceptedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscrepancyAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscrepancyAcceptances_EvaluationCycles_EvaluationCycleId",
                        column: x => x.EvaluationCycleId,
                        principalTable: "EvaluationCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscrepancyAcceptances_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscrepancyAcceptances_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiscrepancyAcceptances_Users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DiscrepancyAcceptances_Users_EvaluatedUserId",
                        column: x => x.EvaluatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EvaluationResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvaluationCycleId = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    EvaluatedUserId = table.Column<int>(type: "integer", nullable: false),
                    CompletedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AverageFinal = table.Column<double>(type: "double precision", nullable: true),
                    EncryptedSnapshot = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationResults_EvaluationCycles_EvaluationCycleId",
                        column: x => x.EvaluationCycleId,
                        principalTable: "EvaluationCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationResults_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EvaluationResults_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EvaluationResults_Users_EvaluatedUserId",
                        column: x => x.EvaluatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscrepancyAcceptances_AcceptedByUserId",
                table: "DiscrepancyAcceptances",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscrepancyAcceptances_EvaluatedUserId",
                table: "DiscrepancyAcceptances",
                column: "EvaluatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscrepancyAcceptances_EvaluationCycleId_TemplateId_Evaluat~",
                table: "DiscrepancyAcceptances",
                columns: new[] { "EvaluationCycleId", "TemplateId", "EvaluatedUserId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscrepancyAcceptances_QuestionId",
                table: "DiscrepancyAcceptances",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscrepancyAcceptances_TemplateId",
                table: "DiscrepancyAcceptances",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_CompletedByUserId",
                table: "EvaluationResults",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_EvaluatedUserId",
                table: "EvaluationResults",
                column: "EvaluatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_EvaluationCycleId_TemplateId_EvaluatedUse~",
                table: "EvaluationResults",
                columns: new[] { "EvaluationCycleId", "TemplateId", "EvaluatedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResults_TemplateId",
                table: "EvaluationResults",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscrepancyAcceptances");

            migrationBuilder.DropTable(
                name: "EvaluationResults");

            migrationBuilder.DropColumn(
                name: "FechaCompletado",
                table: "EvaluationCycles");
        }
    }
}
