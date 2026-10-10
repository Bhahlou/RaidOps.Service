using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidEventCompositionAnnouncementReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompositionAnnouncementChannelId",
                table: "RaidEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompositionAnnouncementMessageId",
                table: "RaidEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompositionAnnouncementChannelId",
                table: "RaidEvents");

            migrationBuilder.DropColumn(
                name: "CompositionAnnouncementMessageId",
                table: "RaidEvents");
        }
    }
}
