using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuildIconHash",
                table: "UserGuilds");

            migrationBuilder.DropColumn(
                name: "GuildName",
                table: "UserGuilds");

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IconHash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserGuilds_GuildId",
                table: "UserGuilds",
                column: "GuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserGuilds_Guilds_GuildId",
                table: "UserGuilds",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserGuilds_Guilds_GuildId",
                table: "UserGuilds");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropIndex(
                name: "IX_UserGuilds_GuildId",
                table: "UserGuilds");

            migrationBuilder.AddColumn<string>(
                name: "GuildIconHash",
                table: "UserGuilds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuildName",
                table: "UserGuilds",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
