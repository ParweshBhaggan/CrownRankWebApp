using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace CrownRankApp.Infrastructure.Migrations;

public partial class AddStripePayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PaymentOperations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Purpose = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Currency = table.Column<string>(type: "text", nullable: false),
                RequestHash = table.Column<string>(type: "text", nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                EntryId = table.Column<Guid>(type: "uuid", nullable: true),
                EntryJson = table.Column<string>(type: "text", nullable: true),
                ReservedUsername = table.Column<string>(type: "text", nullable: true),
                SessionId = table.Column<string>(type: "text", nullable: true),
                CheckoutUrl = table.Column<string>(type: "text", nullable: true),
                PaymentIntentId = table.Column<string>(type: "text", nullable: true),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                FulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AgreementsAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TermsVersion = table.Column<string>(type: "text", nullable: true),
                PrivacyVersion = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_PaymentOperations", row => row.Id));
        migrationBuilder.CreateIndex(name: "IX_PaymentOperations_PaymentIntentId", table: "PaymentOperations", column: "PaymentIntentId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_PaymentOperations_ReservedUsername", table: "PaymentOperations", column: "ReservedUsername", unique: true);
        migrationBuilder.CreateIndex(name: "IX_PaymentOperations_SessionId", table: "PaymentOperations", column: "SessionId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_PaymentOperations_FulfilledAt_Status_CreatedDate", table: "PaymentOperations", columns: new[]
            {
                "FulfilledAt",
                "Status",
                "CreatedDate"
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("PaymentOperations");
    }
}
