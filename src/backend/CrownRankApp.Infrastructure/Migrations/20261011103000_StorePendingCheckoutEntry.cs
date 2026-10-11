using Microsoft.EntityFrameworkCore.Migrations;

namespace CrownRankApp.Infrastructure.Migrations;

public partial class StorePendingCheckoutEntry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PendingEntryJson",
            table: "CheckoutPayments",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PendingEntryJson",
            table: "CheckoutPayments");
    }
}
