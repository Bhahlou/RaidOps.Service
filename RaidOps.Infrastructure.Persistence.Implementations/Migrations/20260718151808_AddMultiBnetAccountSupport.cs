using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiBnetAccountSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Characters_UserDiscordId",
                table: "Characters");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BattleNetAccounts",
                table: "BattleNetAccounts");

            migrationBuilder.AddColumn<string>(
                name: "SourceBnetId",
                table: "Characters",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Backfill: at this point every user still has at most one BattleNetAccounts row
            // (composite key not yet in effect), so this 1:1 correlation is exact.
            migrationBuilder.Sql(
                """
                UPDATE "Characters" c
                SET "SourceBnetId" = b."BnetId"
                FROM "BattleNetAccounts" b
                WHERE c."UserDiscordId" = b."UserDiscordId";
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_BattleNetAccounts",
                table: "BattleNetAccounts",
                columns: new[] { "UserDiscordId", "BnetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_UserDiscordId_SourceBnetId",
                table: "Characters",
                columns: new[] { "UserDiscordId", "SourceBnetId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_BattleNetAccounts_UserDiscordId_SourceBnetId",
                table: "Characters",
                columns: new[] { "UserDiscordId", "SourceBnetId" },
                principalTable: "BattleNetAccounts",
                principalColumns: new[] { "UserDiscordId", "BnetId" },
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_BattleNetAccounts_UserDiscordId_SourceBnetId",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_UserDiscordId_SourceBnetId",
                table: "Characters");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BattleNetAccounts",
                table: "BattleNetAccounts");

            migrationBuilder.DropColumn(
                name: "SourceBnetId",
                table: "Characters");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BattleNetAccounts",
                table: "BattleNetAccounts",
                column: "UserDiscordId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_UserDiscordId",
                table: "Characters",
                column: "UserDiscordId");
        }
    }
}
