using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class ValidationComptesUtilisateur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "date_validation",
                table: "utilisateurs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "statut_compte",
                table: "utilisateurs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "EN_ATTENTE")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                "UPDATE utilisateurs SET statut_compte = 'VALIDE', date_validation = date_creation WHERE statut_compte = 'EN_ATTENTE';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_validation",
                table: "utilisateurs");

            migrationBuilder.DropColumn(
                name: "statut_compte",
                table: "utilisateurs");
        }
    }
}
