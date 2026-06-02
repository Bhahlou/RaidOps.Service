using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaAndGuildAndSpecsToCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Characters",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuildName",
                table: "CharacterExpansionStates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "GuildName",
                table: "CharacterExpansionStates");
        }
    }
}
