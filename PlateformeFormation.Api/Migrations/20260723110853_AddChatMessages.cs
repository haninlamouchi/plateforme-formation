using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlateformeFormation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    expediteur_id = table.Column<int>(type: "int", nullable: false),
                    destinataire_id = table.Column<int>(type: "int", nullable: true),
                    contenu = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    date_envoi = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    lu = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_messages_utilisateurs_destinataire_id",
                        column: x => x.destinataire_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chat_messages_utilisateurs_expediteur_id",
                        column: x => x.expediteur_id,
                        principalTable: "utilisateurs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_utilisateurs_refresh_token",
                table: "utilisateurs",
                column: "refresh_token");

            migrationBuilder.CreateIndex(
                name: "IX_utilisateurs_statut_compte",
                table: "utilisateurs",
                column: "statut_compte");

            migrationBuilder.CreateIndex(
                name: "IX_documents_date_ajout",
                table: "documents",
                column: "date_ajout");

            migrationBuilder.CreateIndex(
                name: "IX_categories_nom",
                table: "categories",
                column: "nom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_date_envoi",
                table: "chat_messages",
                column: "date_envoi");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_destinataire_id",
                table: "chat_messages",
                column: "destinataire_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_expediteur_id_destinataire_id",
                table: "chat_messages",
                columns: new[] { "expediteur_id", "destinataire_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_utilisateurs_refresh_token",
                table: "utilisateurs");

            migrationBuilder.DropIndex(
                name: "IX_utilisateurs_statut_compte",
                table: "utilisateurs");

            migrationBuilder.DropIndex(
                name: "IX_documents_date_ajout",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_categories_nom",
                table: "categories");
        }
    }
}
