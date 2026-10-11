using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddSeenTours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeenTours",
                columns: table => new
                {
                    UserDiscordId = table.Column<string>(type: "text", nullable: false),
                    TourId = table.Column<string>(type: "text", nullable: false),
                    SeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeenTours", x => new { x.UserDiscordId, x.TourId });
                    table.ForeignKey(
                        name: "FK_SeenTours_Users_UserDiscordId",
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
                name: "SeenTours");
        }
    }
}
