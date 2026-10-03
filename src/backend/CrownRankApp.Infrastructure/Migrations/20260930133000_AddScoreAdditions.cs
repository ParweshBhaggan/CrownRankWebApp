using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace CrownRankApp.Infrastructure.Migrations;

public partial class AddScoreAdditions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ScoreAdditions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScoreAdditions", addition => addition.Id);
                table.ForeignKey("FK_ScoreAdditions_Entries_EntryId", addition => addition.EntryId,
                    principalTable: "Entries", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_ScoreAdditions_EntryId", "ScoreAdditions", "EntryId");
        migrationBuilder.CreateIndex("IX_ScoreAdditions_CreatedDate_EntryId", "ScoreAdditions", new[]
            {
                "CreatedDate",
                "EntryId"
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ScoreAdditions");
    }
}
