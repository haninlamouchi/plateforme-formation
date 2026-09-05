using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: `dotnet ef migrations add` also scaffolded DropTable("document_competences") and
            // DropTable("competences") here — pre-existing drift between the current C# model (which
            // no longer defines Competence/DocumentCompetence entities) and the live database schema,
            // unrelated to the two indexes this migration is actually for. Deliberately removed: a
            // migration meant to add indexes must never silently drop tables that may hold real data.
            // If those tables are genuinely dead, that should be its own explicit, reviewed migration.
            migrationBuilder.CreateIndex(
                name: "IX_journal_activite_action_date_action",
                table: "journal_activite",
                columns: new[] { "action", "date_action" });

            migrationBuilder.CreateIndex(
                name: "IX_formations_date_creation",
                table: "formations",
                column: "date_creation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_journal_activite_action_date_action",
                table: "journal_activite");

            migrationBuilder.DropIndex(
                name: "IX_formations_date_creation",
                table: "formations");
        }
    }
}
