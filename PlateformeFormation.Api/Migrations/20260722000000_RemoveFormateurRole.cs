using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFormateurRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE utilisateurs SET role = 'RESPONSABLE_PEDAGOGIQUE' WHERE role = 'FORMATEUR'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cannot restore original FORMATEUR assignments — no-op
        }
    }
}
