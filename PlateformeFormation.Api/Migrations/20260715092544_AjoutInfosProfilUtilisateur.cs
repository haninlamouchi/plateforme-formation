using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AjoutInfosProfilUtilisateur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "date_naissance",
                table: "utilisateurs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "departement",
                table: "utilisateurs",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "discipline",
                table: "utilisateurs",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                table: "utilisateurs",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "telephone",
                table: "utilisateurs",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_naissance",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "departement",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "discipline",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "photo_url",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "telephone",
                table: "utilisateurs");
        }
    }
}
