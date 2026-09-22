using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AttributionRepeatableInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RaidEventAttributions",
                table: "RaidEventAttributions");

            migrationBuilder.AddColumn<int>(
                name: "InstanceIndex",
                table: "RaidEventAttributions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepeatable",
                table: "GuildAttributionDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RaidEventAttributions",
                table: "RaidEventAttributions",
                columns: new[] { "RaidEventId", "AttributionDefinitionCellId", "InstanceIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RaidEventAttributions",
                table: "RaidEventAttributions");

            migrationBuilder.DropColumn(
                name: "InstanceIndex",
                table: "RaidEventAttributions");

            migrationBuilder.DropColumn(
                name: "IsRepeatable",
                table: "GuildAttributionDefinitions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RaidEventAttributions",
                table: "RaidEventAttributions",
                columns: new[] { "RaidEventId", "AttributionDefinitionCellId" });
        }
    }
}
