using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace evalflow_backend_api.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class BackfillEvaluationTypeTo360 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before this feature exposed `TipoEvaluación` through the API,
            // every cycle silently behaved as a full 360° evaluation
            // (GenerateSubmissionsHandler always created both the
            // self-evaluation and the manager-evaluates-subordinate
            // submissions, regardless of the stored value). Every existing
            // row is stuck at the entity's old default, Auto (0), which
            // — now that the handler actually respects the type — would
            // silently turn into "self-evaluation only" for cycles that were
            // always meant to be 360°. Backfill to 360 (2) to preserve the
            // behavior every existing cycle already had.
            migrationBuilder.Sql("UPDATE \"EvaluationCycles\" SET \"TipoEvaluación\" = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"EvaluationCycles\" SET \"TipoEvaluación\" = 0;");
        }
    }
}
