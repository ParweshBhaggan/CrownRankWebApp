using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrownRank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contributions_creators_CreatorId",
                schema: "CrownrankSchema",
                table: "contributions");

            migrationBuilder.DropColumn(
                name: "Location",
                schema: "CrownrankSchema",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "AmountCents",
                schema: "CrownrankSchema",
                table: "contributions");

            migrationBuilder.AddColumn<Guid>(
                name: "EntryReference",
                schema: "CrownrankSchema",
                table: "creators",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                schema: "CrownrankSchema",
                table: "creators",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningAmount",
                schema: "CrownrankSchema",
                table: "creators",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                schema: "CrownrankSchema",
                table: "contributions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_creators_EntryReference",
                schema: "CrownrankSchema",
                table: "creators",
                column: "EntryReference",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_contributions_creators_CreatorId",
                schema: "CrownrankSchema",
                table: "contributions",
                column: "CreatorId",
                principalSchema: "CrownrankSchema",
                principalTable: "creators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contributions_creators_CreatorId",
                schema: "CrownrankSchema",
                table: "contributions");

            migrationBuilder.DropIndex(
                name: "IX_creators_EntryReference",
                schema: "CrownrankSchema",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "EntryReference",
                schema: "CrownrankSchema",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                schema: "CrownrankSchema",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "OpeningAmount",
                schema: "CrownrankSchema",
                table: "creators");

            migrationBuilder.DropColumn(
                name: "Amount",
                schema: "CrownrankSchema",
                table: "contributions");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                schema: "CrownrankSchema",
                table: "creators",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AmountCents",
                schema: "CrownrankSchema",
                table: "contributions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddForeignKey(
                name: "FK_contributions_creators_CreatorId",
                schema: "CrownrankSchema",
                table: "contributions",
                column: "CreatorId",
                principalSchema: "CrownrankSchema",
                principalTable: "creators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
