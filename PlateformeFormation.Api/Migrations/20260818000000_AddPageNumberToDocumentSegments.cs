using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PlateformeFormation.Api.Data;

#nullable disable

namespace PlateformeFormation.Api.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260818000000_AddPageNumberToDocumentSegments")]
public partial class AddPageNumberToDocumentSegments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "numero_page",
            table: "document_segments",
            type: "int",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "numero_page", table: "document_segments");
    }
}
