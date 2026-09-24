using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddClarificationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClarificationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvaluationCycleId = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: true),
                    EvaluatedUserId = table.Column<int>(type: "integer", nullable: false),
                    ManagerUserId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvaluatedResponseEncrypted = table.Column<string>(type: "text", nullable: true),
                    EvaluatedRespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ManagerResponseEncrypted = table.Column<string>(type: "text", nullable: true),
                    ManagerRespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClarificationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_EvaluationCycles_EvaluationCycleId",
                        column: x => x.EvaluationCycleId,
                        principalTable: "EvaluationCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_Users_EvaluatedUserId",
                        column: x => x.EvaluatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_Users_ManagerUserId",
                        column: x => x.ManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClarificationRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_EvaluatedUserId",
                table: "ClarificationRequests",
                column: "EvaluatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_EvaluationCycleId_EvaluatedUserId",
                table: "ClarificationRequests",
                columns: new[] { "EvaluationCycleId", "EvaluatedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_ManagerUserId",
                table: "ClarificationRequests",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_QuestionId",
                table: "ClarificationRequests",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_RequestedByUserId",
                table: "ClarificationRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClarificationRequests_TemplateId",
                table: "ClarificationRequests",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClarificationRequests");
        }
    }
}
