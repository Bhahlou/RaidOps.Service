using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidSignups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignupCallAnnouncementChannelId",
                table: "RaidEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignupCallAnnouncementMessageId",
                table: "RaidEvents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RaidSignups",
                columns: table => new
                {
                    RaidEventId = table.Column<int>(type: "integer", nullable: false),
                    UserDiscordId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidSignups", x => new { x.RaidEventId, x.UserDiscordId });
                    table.ForeignKey(
                        name: "FK_RaidSignups_RaidEvents_RaidEventId",
                        column: x => x.RaidEventId,
                        principalTable: "RaidEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaidSignups_Users_UserDiscordId",
                        column: x => x.UserDiscordId,
                        principalTable: "Users",
                        principalColumn: "DiscordId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaidSignups_UserDiscordId",
                table: "RaidSignups",
                column: "UserDiscordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaidSignups");

            migrationBuilder.DropColumn(
                name: "SignupCallAnnouncementChannelId",
                table: "RaidEvents");

            migrationBuilder.DropColumn(
                name: "SignupCallAnnouncementMessageId",
                table: "RaidEvents");
        }
    }
}
