using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// UpdateDeleteBehaviorToCascade never created Companies.Cif, DireccionFiscal and Sector in its Up (only its
    /// Down drops them): production got them by hand, so a database built from the migrations (local Postgres,
    /// a new Neon) lacked them. IF NOT EXISTS keeps this a no-op where they already exist.
    /// </summary>
    public partial class AddMissingCompanyFiscalColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "Cif" character varying(15) NOT NULL DEFAULT '';
                ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "DireccionFiscal" character varying(150);
                ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "Sector" character varying(50) NOT NULL DEFAULT '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing: the columns belong to the model since the beginning (see UpdateDeleteBehaviorToCascade.Down).
        }
    }
}
