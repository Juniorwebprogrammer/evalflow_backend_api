using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateFavoriteLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FechÇaInicio",
                table: "Templates",
                newName: "FechaInicio");

            migrationBuilder.RenameColumn(
                name: "LogoURL",
                table: "Companies",
                newName: "LogoUrl");

            migrationBuilder.CreateTable(
                name: "TemplateFavoriteLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateFavoriteLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateFavoriteLists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateTemplateFavoriteList",
                columns: table => new
                {
                    ListasFavoritasId = table.Column<int>(type: "integer", nullable: false),
                    TemplatesId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateTemplateFavoriteList", x => new { x.ListasFavoritasId, x.TemplatesId });
                    table.ForeignKey(
                        name: "FK_TemplateTemplateFavoriteList_TemplateFavoriteLists_ListasFa~",
                        column: x => x.ListasFavoritasId,
                        principalTable: "TemplateFavoriteLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TemplateTemplateFavoriteList_Templates_TemplatesId",
                        column: x => x.TemplatesId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFavoriteLists_UserId",
                table: "TemplateFavoriteLists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateTemplateFavoriteList_TemplatesId",
                table: "TemplateTemplateFavoriteList",
                column: "TemplatesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateTemplateFavoriteList");

            migrationBuilder.DropTable(
                name: "TemplateFavoriteLists");

            migrationBuilder.RenameColumn(
                name: "FechaInicio",
                table: "Templates",
                newName: "FechÇaInicio");

            migrationBuilder.RenameColumn(
                name: "LogoUrl",
                table: "Companies",
                newName: "LogoURL");
        }
    }
}
