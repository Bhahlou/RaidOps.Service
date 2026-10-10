using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddSpellAvailabilityAndBranchWagoSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedBuildDate",
                table: "Branches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSyncedBuildVersion",
                table: "Branches",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WagoProductCode",
                table: "Branches",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpellAvailabilities",
                columns: table => new
                {
                    SpellId = table.Column<int>(type: "integer", nullable: false),
                    ExpansionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpellAvailabilities", x => new { x.SpellId, x.ExpansionId });
                    table.ForeignKey(
                        name: "FK_SpellAvailabilities_Expansions_ExpansionId",
                        column: x => x.ExpansionId,
                        principalTable: "Expansions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpellAvailabilities_Spells_SpellId",
                        column: x => x.SpellId,
                        principalTable: "Spells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill from the column we're about to drop before dropping it, so every spell
            // already seeded under a single ExpansionId keeps showing up in that expansion's
            // attribution picker — no reseed needed, no data lost.
            migrationBuilder.Sql(
                """
                INSERT INTO "SpellAvailabilities" ("SpellId", "ExpansionId")
                SELECT "Id", "ExpansionId" FROM "Spells";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Spells_Expansions_ExpansionId",
                table: "Spells");

            migrationBuilder.DropIndex(
                name: "IX_Spells_ExpansionId",
                table: "Spells");

            migrationBuilder.DropColumn(
                name: "ExpansionId",
                table: "Spells");

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LastSyncedBuildDate", "LastSyncedBuildVersion", "WagoProductCode" },
                values: new object[] { null, null, "wow" });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "LastSyncedBuildDate", "LastSyncedBuildVersion", "WagoProductCode" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "LastSyncedBuildDate", "LastSyncedBuildVersion", "WagoProductCode" },
                values: new object[] { null, null, "wow_classic" });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "LastSyncedBuildDate", "LastSyncedBuildVersion", "WagoProductCode" },
                values: new object[] { null, null, "wow_anniversary" });

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "LastSyncedBuildDate", "LastSyncedBuildVersion", "WagoProductCode" },
                values: new object[] { null, null, "wow_classic_beta" });

            migrationBuilder.CreateIndex(
                name: "IX_SpellAvailabilities_ExpansionId",
                table: "SpellAvailabilities",
                column: "ExpansionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpellAvailabilities");

            migrationBuilder.DropColumn(
                name: "LastSyncedBuildDate",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "LastSyncedBuildVersion",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "WagoProductCode",
                table: "Branches");

            migrationBuilder.AddColumn<int>(
                name: "ExpansionId",
                table: "Spells",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Spells_ExpansionId",
                table: "Spells",
                column: "ExpansionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Spells_Expansions_ExpansionId",
                table: "Spells",
                column: "ExpansionId",
                principalTable: "Expansions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
