using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Raw SQL required: EF Core's AddColumn does not emit DEFAULT for Npgsql array types,
            // which would cause a NOT NULL violation on existing rows.
            migrationBuilder.Sql(@"ALTER TABLE ""Guilds"" ADD COLUMN IF NOT EXISTS ""AllowedRosterRoleIds"" text[] NOT NULL DEFAULT '{}'");

            migrationBuilder.AddColumn<int>(
                name: "RosterMode",
                table: "Guilds",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Timezone",
                table: "Guilds",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedRosterRoleIds",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "RosterMode",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "Timezone",
                table: "Guilds");
        }
    }
}
