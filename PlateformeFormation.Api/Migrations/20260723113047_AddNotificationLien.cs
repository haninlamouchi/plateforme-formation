using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationLien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lien",
                table: "notifications",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "lien",
                table: "notifications");
        }
    }
}
