using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAllowedRolesWithMinRosterRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedRosterRoleIds",
                table: "Guilds");

            migrationBuilder.AddColumn<string>(
                name: "MinRosterRoleId",
                table: "Guilds",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinRosterRoleId",
                table: "Guilds");

            migrationBuilder.Sql(@"ALTER TABLE ""Guilds"" ADD COLUMN IF NOT EXISTS ""AllowedRosterRoleIds"" text[] NOT NULL DEFAULT '{}'");
        }
    }
}
