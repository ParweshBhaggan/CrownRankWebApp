using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrownRank.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ArchivedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfileImageKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    AgreementAcceptance_TermsVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AgreementAcceptance_PrivacyPolicyVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AgreementAcceptance_RulesVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AgreementAcceptance_AcceptedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    InitialContributionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    PublishedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    HiddenAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ArchivedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entries_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpectedAmount_AmountInMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpectedAmount_Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Purpose = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ConfirmedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAttempts_Entries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RankingContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount_AmountInMinorUnits = table.Column<long>(type: "INTEGER", nullable: false),
                    Amount_Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    PaymentProvider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PaymentReference = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfirmedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ExclusionReason = table.Column<int>(type: "INTEGER", nullable: true),
                    ExcludedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RankingContributions_Entries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SocialMediaLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Platform = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    CustomPlatformName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EntryId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialMediaLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialMediaLinks_Entries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "Entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Status",
                table: "Categories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Entries_CategoryId_Status",
                table: "Entries",
                columns: new[] { "CategoryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_EntryId_State",
                table: "PaymentAttempts",
                columns: new[] { "EntryId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Provider_Reference",
                table: "PaymentAttempts",
                columns: new[] { "Provider", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankingContributions_EntryId_Status_ConfirmedAtUtc",
                table: "RankingContributions",
                columns: new[] { "EntryId", "Status", "ConfirmedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RankingContributions_PaymentProvider_PaymentReference",
                table: "RankingContributions",
                columns: new[] { "PaymentProvider", "PaymentReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SocialMediaLinks_EntryId",
                table: "SocialMediaLinks",
                column: "EntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAttempts");

            migrationBuilder.DropTable(
                name: "RankingContributions");

            migrationBuilder.DropTable(
                name: "SocialMediaLinks");

            migrationBuilder.DropTable(
                name: "Entries");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
