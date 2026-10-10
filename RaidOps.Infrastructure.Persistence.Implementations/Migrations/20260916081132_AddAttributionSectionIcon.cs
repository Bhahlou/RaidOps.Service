using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddAttributionSectionIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SectionIconSource",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SectionRaidMarker",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionSpellId",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionStaticRole",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildAttributionDefinitions_SectionSpellId",
                table: "GuildAttributionDefinitions",
                column: "SectionSpellId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildAttributionDefinitions_Spells_SectionSpellId",
                table: "GuildAttributionDefinitions",
                column: "SectionSpellId",
                principalTable: "Spells",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildAttributionDefinitions_Spells_SectionSpellId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_GuildAttributionDefinitions_SectionSpellId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "SectionIconSource",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "SectionRaidMarker",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "SectionSpellId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "SectionStaticRole",
                table: "GuildAttributionDefinitions");
        }
    }
}
