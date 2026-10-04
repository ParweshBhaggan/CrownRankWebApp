using Microsoft.EntityFrameworkCore.Migrations;

namespace CrownRankApp.Infrastructure.Migrations;

public partial class AddPaymentRecoveryChecks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "LastCheckedAt", table: "CheckoutPayments", type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_CheckoutPayments_FulfilledEntryId_LastCheckedAt", table: "CheckoutPayments", columns: new[]
            {
                "FulfilledEntryId",
                "LastCheckedAt"
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_CheckoutPayments_FulfilledEntryId_LastCheckedAt", table: "CheckoutPayments");
        migrationBuilder.DropColumn(name: "LastCheckedAt", table: "CheckoutPayments");
    }
}
