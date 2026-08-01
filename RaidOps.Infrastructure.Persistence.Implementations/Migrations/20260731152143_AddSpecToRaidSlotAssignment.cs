using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecToRaidSlotAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpecId",
                table: "RaidSlotAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill any pre-existing assignment with the character's main raid spec before the
            // FK constraint below is added — a bare defaultValue of 0 would violate it otherwise.
            migrationBuilder.Sql("""
                UPDATE "RaidSlotAssignments" a
                SET "SpecId" = crs."SpecId"
                FROM "CharacterRaidSpecs" crs
                WHERE crs."CharacterId" = a."CharacterId" AND crs."IsMain" = true;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RaidSlotAssignments_SpecId",
                table: "RaidSlotAssignments",
                column: "SpecId");

            migrationBuilder.AddForeignKey(
                name: "FK_RaidSlotAssignments_Specs_SpecId",
                table: "RaidSlotAssignments",
                column: "SpecId",
                principalTable: "Specs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidSlotAssignments_Specs_SpecId",
                table: "RaidSlotAssignments");

            migrationBuilder.DropIndex(
                name: "IX_RaidSlotAssignments_SpecId",
                table: "RaidSlotAssignments");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "RaidSlotAssignments");
        }
    }
}
