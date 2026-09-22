using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidAttributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Spells",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ExpansionId = table.Column<int>(type: "integer", nullable: false),
                    NameEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameFr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NameDe = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spells_Expansions_ExpansionId",
                        column: x => x.ExpansionId,
                        principalTable: "Expansions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuildAttributionDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Section = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IconSource = table.Column<int>(type: "integer", nullable: false),
                    SpellId = table.Column<int>(type: "integer", nullable: true),
                    RaidMarker = table.Column<int>(type: "integer", nullable: true),
                    SlotCount = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByDiscordId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAttributionDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildAttributionDefinitions_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildAttributionDefinitions_Spells_SpellId",
                        column: x => x.SpellId,
                        principalTable: "Spells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaidEventAttributions",
                columns: table => new
                {
                    RaidEventId = table.Column<int>(type: "integer", nullable: false),
                    GuildAttributionDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    SlotIndex = table.Column<int>(type: "integer", nullable: false),
                    CharacterId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedByDiscordId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidEventAttributions", x => new { x.RaidEventId, x.GuildAttributionDefinitionId, x.SlotIndex });
                    table.ForeignKey(
                        name: "FK_RaidEventAttributions_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaidEventAttributions_GuildAttributionDefinitions_GuildAttr~",
                        column: x => x.GuildAttributionDefinitionId,
                        principalTable: "GuildAttributionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaidEventAttributions_RaidEvents_RaidEventId",
                        column: x => x.RaidEventId,
                        principalTable: "RaidEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildAttributionDefinitions_GuildId_SortOrder",
                table: "GuildAttributionDefinitions",
                columns: new[] { "GuildId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildAttributionDefinitions_SpellId",
                table: "GuildAttributionDefinitions",
                column: "SpellId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidEventAttributions_CharacterId",
                table: "RaidEventAttributions",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidEventAttributions_GuildAttributionDefinitionId",
                table: "RaidEventAttributions",
                column: "GuildAttributionDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Spells_ExpansionId",
                table: "Spells",
                column: "ExpansionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaidEventAttributions");

            migrationBuilder.DropTable(
                name: "GuildAttributionDefinitions");

            migrationBuilder.DropTable(
                name: "Spells");
        }
    }
}
