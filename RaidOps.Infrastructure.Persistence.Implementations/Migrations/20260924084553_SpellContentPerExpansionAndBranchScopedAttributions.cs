using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class SpellContentPerExpansionAndBranchScopedAttributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GuildAttributionDefinitions_GuildId_SortOrder",
                table: "GuildAttributionDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                table: "SpellAvailabilities",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameDe",
                table: "SpellAvailabilities",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "SpellAvailabilities",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameFr",
                table: "SpellAvailabilities",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            // Every (spell, expansion) row starts from the spell's current global name/icon — the last
            // sync to write it. That is only a starting point: a forced re-sync rewrites each
            // expansion's rows with its own branch's real names.
            migrationBuilder.Sql(
                """
                UPDATE "SpellAvailabilities" sa
                SET "NameEn" = s."NameEn", "NameFr" = s."NameFr", "NameDe" = s."NameDe", "IconUrl" = s."IconUrl"
                FROM "Spells" s
                WHERE s."Id" = sa."SpellId";
                """);

            migrationBuilder.DropColumn(
                name: "IconUrl",
                table: "Spells");

            migrationBuilder.DropColumn(
                name: "NameDe",
                table: "Spells");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Spells");

            migrationBuilder.DropColumn(
                name: "NameFr",
                table: "Spells");

            // Attribution templates used to be guild-wide with no branch to attach existing rows to.
            // The feature wasn't in real use yet, so they're dropped (cascading to cells and per-event
            // fills) rather than guessed onto a branch — each guild rebuilds one template per branch.
            migrationBuilder.Sql("""DELETE FROM "GuildAttributionDefinitions";""");

            migrationBuilder.AddColumn<int>(
                name: "GuildBranchId",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_GuildAttributionDefinitions_GuildBranchId_SortOrder",
                table: "GuildAttributionDefinitions",
                columns: new[] { "GuildBranchId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildAttributionDefinitions_GuildId",
                table: "GuildAttributionDefinitions",
                column: "GuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildAttributionDefinitions_GuildBranches_GuildBranchId",
                table: "GuildAttributionDefinitions",
                column: "GuildBranchId",
                principalTable: "GuildBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildAttributionDefinitions_GuildBranches_GuildBranchId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_GuildAttributionDefinitions_GuildBranchId_SortOrder",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_GuildAttributionDefinitions_GuildId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "IconUrl",
                table: "SpellAvailabilities");

            migrationBuilder.DropColumn(
                name: "NameDe",
                table: "SpellAvailabilities");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "SpellAvailabilities");

            migrationBuilder.DropColumn(
                name: "NameFr",
                table: "SpellAvailabilities");

            migrationBuilder.DropColumn(
                name: "GuildBranchId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                table: "Spells",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameDe",
                table: "Spells",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Spells",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameFr",
                table: "Spells",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_GuildAttributionDefinitions_GuildId_SortOrder",
                table: "GuildAttributionDefinitions",
                columns: new[] { "GuildId", "SortOrder" });
        }
    }
}
