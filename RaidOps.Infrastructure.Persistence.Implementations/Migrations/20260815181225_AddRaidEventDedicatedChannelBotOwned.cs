using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidEventDedicatedChannelBotOwned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DedicatedAnnouncementChannelIsBotOwned",
                table: "RaidEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DedicatedAnnouncementChannelIsBotOwned",
                table: "RaidEvents");
        }
    }
}
