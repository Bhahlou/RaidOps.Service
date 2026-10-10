using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddSeenChangelogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeenChangelogEntries",
                columns: table => new
                {
                    UserDiscordId = table.Column<string>(type: "text", nullable: false),
                    EntryId = table.Column<string>(type: "text", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeenChangelogEntries", x => new { x.UserDiscordId, x.EntryId });
                    table.ForeignKey(
                        name: "FK_SeenChangelogEntries_Users_UserDiscordId",
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
                name: "SeenChangelogEntries");
        }
    }
}
