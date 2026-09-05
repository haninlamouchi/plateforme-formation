using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class SecurityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                table: "utilisateurs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_google_user",
                table: "utilisateurs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "lockout_until",
                table: "utilisateurs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refresh_token",
                table: "utilisateurs",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "refresh_token_expiry",
                table: "utilisateurs",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "is_google_user",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "lockout_until",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "refresh_token",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "refresh_token_expiry",
                table: "utilisateurs");
        }
    }
}
