using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAnswerForEncryption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvaluationSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvaluationCycleId = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    EvaluatedUserId = table.Column<int>(type: "integer", nullable: false),
                    RespondentUserId = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationSubmissions_EvaluationCycles_EvaluationCycleId",
                        column: x => x.EvaluationCycleId,
                        principalTable: "EvaluationCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationSubmissions_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationSubmissions_Users_EvaluatedUserId",
                        column: x => x.EvaluatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationSubmissions_Users_RespondentUserId",
                        column: x => x.RespondentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Answers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvaluationSubmissionId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    ValorTexto = table.Column<string>(type: "text", nullable: true),
                    ValorNumerico = table.Column<int>(type: "integer", nullable: true),
                    OpcionesSeleccionadas = table.Column<List<string>>(type: "text[]", nullable: true),
                    EncryptedPayload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Answers_EvaluationSubmissions_EvaluationSubmissionId",
                        column: x => x.EvaluationSubmissionId,
                        principalTable: "EvaluationSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Answers_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Answers_EvaluationSubmissionId",
                table: "Answers",
                column: "EvaluationSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Answers_QuestionId",
                table: "Answers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationSubmissions_EvaluatedUserId",
                table: "EvaluationSubmissions",
                column: "EvaluatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationSubmissions_EvaluationCycleId",
                table: "EvaluationSubmissions",
                column: "EvaluationCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationSubmissions_RespondentUserId",
                table: "EvaluationSubmissions",
                column: "RespondentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationSubmissions_TemplateId",
                table: "EvaluationSubmissions",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Answers");

            migrationBuilder.DropTable(
                name: "EvaluationSubmissions");
        }
    }
}
