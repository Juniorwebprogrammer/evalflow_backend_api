using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTemplateTopicAndAddQuestionTopic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Topic",
                table: "Templates");

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "Questions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Topic",
                table: "Questions");

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "Templates",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
