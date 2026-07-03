using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficerThresholdAndNotificationDismissal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MinOfficerRoleId",
                table: "Guilds",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationDismissals",
                columns: table => new
                {
                    UserDiscordId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDismissals", x => new { x.UserDiscordId, x.Type, x.GuildId });
                    table.ForeignKey(
                        name: "FK_NotificationDismissals_Users_UserDiscordId",
                        column: x => x.UserDiscordId,
                        principalTable: "Users",
                        principalColumn: "DiscordId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDismissals");

            migrationBuilder.DropColumn(
                name: "MinOfficerRoleId",
                table: "Guilds");
        }
    }
}
