using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class ReworkAttributionsIntoCells : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildAttributionDefinitions_Spells_SpellId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_RaidEventAttributions_GuildAttributionDefinitions_GuildAttr~",
                table: "RaidEventAttributions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RaidEventAttributions",
                table: "RaidEventAttributions");

            migrationBuilder.DropIndex(
                name: "IX_RaidEventAttributions_GuildAttributionDefinitionId",
                table: "RaidEventAttributions");

            migrationBuilder.DropIndex(
                name: "IX_GuildAttributionDefinitions_SpellId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "IconSource",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "RaidMarker",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "SlotCount",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropColumn(
                name: "SpellId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.RenameColumn(
                name: "SlotIndex",
                table: "RaidEventAttributions",
                newName: "AttributionDefinitionCellId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RaidEventAttributions",
                table: "RaidEventAttributions",
                columns: new[] { "RaidEventId", "AttributionDefinitionCellId" });

            migrationBuilder.CreateTable(
                name: "AttributionDefinitionCells",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildAttributionDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    CellIndex = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IconSource = table.Column<int>(type: "integer", nullable: false),
                    SpellId = table.Column<int>(type: "integer", nullable: true),
                    RaidMarker = table.Column<int>(type: "integer", nullable: true),
                    StaticRole = table.Column<int>(type: "integer", nullable: true),
                    SlotLabel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RequiredClassId = table.Column<int>(type: "integer", nullable: true),
                    RequiredRole = table.Column<int>(type: "integer", nullable: true),
                    RequiredSpecId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributionDefinitionCells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributionDefinitionCells_GuildAttributionDefinitions_Guil~",
                        column: x => x.GuildAttributionDefinitionId,
                        principalTable: "GuildAttributionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttributionDefinitionCells_Specs_RequiredSpecId",
                        column: x => x.RequiredSpecId,
                        principalTable: "Specs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttributionDefinitionCells_Spells_SpellId",
                        column: x => x.SpellId,
                        principalTable: "Spells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttributionDefinitionCells_WowClasses_RequiredClassId",
                        column: x => x.RequiredClassId,
                        principalTable: "WowClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaidEventAttributions_AttributionDefinitionCellId",
                table: "RaidEventAttributions",
                column: "AttributionDefinitionCellId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributionDefinitionCells_GuildAttributionDefinitionId_Cel~",
                table: "AttributionDefinitionCells",
                columns: new[] { "GuildAttributionDefinitionId", "CellIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_AttributionDefinitionCells_RequiredClassId",
                table: "AttributionDefinitionCells",
                column: "RequiredClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributionDefinitionCells_RequiredSpecId",
                table: "AttributionDefinitionCells",
                column: "RequiredSpecId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributionDefinitionCells_SpellId",
                table: "AttributionDefinitionCells",
                column: "SpellId");

            migrationBuilder.AddForeignKey(
                name: "FK_RaidEventAttributions_AttributionDefinitionCells_Attributio~",
                table: "RaidEventAttributions",
                column: "AttributionDefinitionCellId",
                principalTable: "AttributionDefinitionCells",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidEventAttributions_AttributionDefinitionCells_Attributio~",
                table: "RaidEventAttributions");

            migrationBuilder.DropTable(
                name: "AttributionDefinitionCells");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RaidEventAttributions",
                table: "RaidEventAttributions");

            migrationBuilder.DropIndex(
                name: "IX_RaidEventAttributions_AttributionDefinitionCellId",
                table: "RaidEventAttributions");

            migrationBuilder.RenameColumn(
                name: "AttributionDefinitionCellId",
                table: "RaidEventAttributions",
                newName: "SlotIndex");

            migrationBuilder.AddColumn<int>(
                name: "IconSource",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RaidMarker",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlotCount",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SpellId",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_RaidEventAttributions",
                table: "RaidEventAttributions",
                columns: new[] { "RaidEventId", "GuildAttributionDefinitionId", "SlotIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_RaidEventAttributions_GuildAttributionDefinitionId",
                table: "RaidEventAttributions",
                column: "GuildAttributionDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildAttributionDefinitions_SpellId",
                table: "GuildAttributionDefinitions",
                column: "SpellId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildAttributionDefinitions_Spells_SpellId",
                table: "GuildAttributionDefinitions",
                column: "SpellId",
                principalTable: "Spells",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RaidEventAttributions_GuildAttributionDefinitions_GuildAttr~",
                table: "RaidEventAttributions",
                column: "GuildAttributionDefinitionId",
                principalTable: "GuildAttributionDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
