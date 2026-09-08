using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrownRank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrownrankSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "CrownrankSchema");

            migrationBuilder.CreateTable(
                name: "creators",
                schema: "CrownrankSchema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LastName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ImageStorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contributions",
                schema: "CrownrankSchema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountCents = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contributions_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "CrownrankSchema",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "social_profiles",
                schema: "CrownrankSchema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_social_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_social_profiles_creators_CreatorId",
                        column: x => x.CreatorId,
                        principalSchema: "CrownrankSchema",
                        principalTable: "creators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contributions_CreatorId_ConfirmedAt",
                schema: "CrownrankSchema",
                table: "contributions",
                columns: new[] { "CreatorId", "ConfirmedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contributions_PaymentReference",
                schema: "CrownrankSchema",
                table: "contributions",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creators_Username",
                schema: "CrownrankSchema",
                table: "creators",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_social_profiles_CreatorId_Platform",
                schema: "CrownrankSchema",
                table: "social_profiles",
                columns: new[] { "CreatorId", "Platform" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contributions",
                schema: "CrownrankSchema");

            migrationBuilder.DropTable(
                name: "social_profiles",
                schema: "CrownrankSchema");

            migrationBuilder.DropTable(
                name: "creators",
                schema: "CrownrankSchema");
        }
    }
}
