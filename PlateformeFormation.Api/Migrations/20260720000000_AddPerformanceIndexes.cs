using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Unique constraint on category name to prevent duplicates and race conditions
            migrationBuilder.CreateIndex(
                name: "IX_categories_nom",
                table: "categories",
                column: "nom",
                unique: true);

            // Index on documents.date_ajout for ORDER BY performance
            migrationBuilder.CreateIndex(
                name: "IX_documents_date_ajout",
                table: "documents",
                column: "date_ajout");

            // Index on utilisateurs.statut_compte for pending-user list queries
            migrationBuilder.CreateIndex(
                name: "IX_utilisateurs_statut_compte",
                table: "utilisateurs",
                column: "statut_compte");

            // Index on utilisateurs.refresh_token for token-refresh lookups
            migrationBuilder.CreateIndex(
                name: "IX_utilisateurs_refresh_token",
                table: "utilisateurs",
                column: "refresh_token");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categories_nom",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "IX_documents_date_ajout",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_utilisateurs_statut_compte",
                table: "utilisateurs");

            migrationBuilder.DropIndex(
                name: "IX_utilisateurs_refresh_token",
                table: "utilisateurs");
        }
    }
}
