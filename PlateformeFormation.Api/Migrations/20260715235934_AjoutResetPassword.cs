using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AjoutResetPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reset_password_token",
                table: "utilisateurs",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "reset_password_token_expiry",
                table: "utilisateurs",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reset_password_token",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "reset_password_token_expiry",
                table: "utilisateurs");
        }
    }
}
