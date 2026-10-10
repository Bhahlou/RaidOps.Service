using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class ChangeCharacterSourceBnetIdCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_BattleNetAccounts_UserDiscordId_SourceBnetId",
                table: "Characters");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_BattleNetAccounts_UserDiscordId_SourceBnetId",
                table: "Characters",
                columns: new[] { "UserDiscordId", "SourceBnetId" },
                principalTable: "BattleNetAccounts",
                principalColumns: new[] { "UserDiscordId", "BnetId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_BattleNetAccounts_UserDiscordId_SourceBnetId",
                table: "Characters");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_BattleNetAccounts_UserDiscordId_SourceBnetId",
                table: "Characters",
                columns: new[] { "UserDiscordId", "SourceBnetId" },
                principalTable: "BattleNetAccounts",
                principalColumns: new[] { "UserDiscordId", "BnetId" },
                onDelete: ReferentialAction.SetNull);
        }
    }
}
