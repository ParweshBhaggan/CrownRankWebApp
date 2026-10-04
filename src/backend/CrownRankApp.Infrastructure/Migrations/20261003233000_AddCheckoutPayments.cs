using Microsoft.EntityFrameworkCore.Migrations;

namespace CrownRankApp.Infrastructure.Migrations;

public partial class AddCheckoutPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "CheckoutPayments", columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Currency = table.Column<string>(type: "text", nullable: false),
                Purpose = table.Column<string>(type: "text", nullable: false),
                BoostEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                SessionId = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                FrontendUrl = table.Column<string>(type: "text", nullable: false),
                LiveMode = table.Column<bool>(type: "boolean", nullable: false),
                PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                FulfilledEntryId = table.Column<Guid>(type: "uuid", nullable: true),
            }, constraints: table => table.PrimaryKey("PK_CheckoutPayments", row => row.Id));
        migrationBuilder.CreateIndex(name: "IX_CheckoutPayments_SessionId", table: "CheckoutPayments", column: "SessionId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("CheckoutPayments");
    }
}
