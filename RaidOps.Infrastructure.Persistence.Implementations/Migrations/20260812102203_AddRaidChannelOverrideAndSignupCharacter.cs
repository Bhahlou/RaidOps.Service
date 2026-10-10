using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidChannelOverrideAndSignupCharacter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CharacterId",
                table: "RaidSignups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedicatedAnnouncementChannelId",
                table: "RaidSeries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DedicatedAnnouncementChannelId",
                table: "RaidEvents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidSignups_CharacterId",
                table: "RaidSignups",
                column: "CharacterId");

            migrationBuilder.AddForeignKey(
                name: "FK_RaidSignups_Characters_CharacterId",
                table: "RaidSignups",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidSignups_Characters_CharacterId",
                table: "RaidSignups");

            migrationBuilder.DropIndex(
                name: "IX_RaidSignups_CharacterId",
                table: "RaidSignups");

            migrationBuilder.DropColumn(
                name: "CharacterId",
                table: "RaidSignups");

            migrationBuilder.DropColumn(
                name: "DedicatedAnnouncementChannelId",
                table: "RaidSeries");

            migrationBuilder.DropColumn(
                name: "DedicatedAnnouncementChannelId",
                table: "RaidEvents");
        }
    }
}
