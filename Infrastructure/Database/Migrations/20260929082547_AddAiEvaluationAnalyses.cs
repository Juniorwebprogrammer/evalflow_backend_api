using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAiEvaluationAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAiAnalysesPerMonth",
                table: "Plans",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiEvaluationAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmpresaID = table.Column<int>(type: "integer", nullable: false),
                    EvaluationCycleId = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    EvaluatedUserId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InputEncrypted = table.Column<string>(type: "text", nullable: false),
                    ResultEncrypted = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PromptTokens = table.Column<int>(type: "integer", nullable: true),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMensaje = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaCompletado = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiEvaluationAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiEvaluationAnalyses_Companies_EmpresaID",
                        column: x => x.EmpresaID,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiEvaluationAnalyses_EvaluationCycles_EvaluationCycleId",
                        column: x => x.EvaluationCycleId,
                        principalTable: "EvaluationCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiEvaluationAnalyses_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiEvaluationAnalyses_Users_EvaluatedUserId",
                        column: x => x.EvaluatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AiEvaluationAnalyses_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 1,
                column: "MaxAiAnalysesPerMonth",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 2,
                column: "MaxAiAnalysesPerMonth",
                value: 30);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: 3,
                column: "MaxAiAnalysesPerMonth",
                value: 150);

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationAnalyses_EmpresaID_FechaCreacion",
                table: "AiEvaluationAnalyses",
                columns: new[] { "EmpresaID", "FechaCreacion" });

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationAnalyses_Estado_NextAttemptAt",
                table: "AiEvaluationAnalyses",
                columns: new[] { "Estado", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationAnalyses_EvaluatedUserId",
                table: "AiEvaluationAnalyses",
                column: "EvaluatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationAnalyses_EvaluationCycleId_TemplateId_Evaluated~",
                table: "AiEvaluationAnalyses",
                columns: new[] { "EvaluationCycleId", "TemplateId", "EvaluatedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationAnalyses_RequestedByUserId",
                table: "AiEvaluationAnalyses",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationAnalyses_TemplateId",
                table: "AiEvaluationAnalyses",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiEvaluationAnalyses");

            migrationBuilder.DropColumn(
                name: "MaxAiAnalysesPerMonth",
                table: "Plans");
        }
    }
}
