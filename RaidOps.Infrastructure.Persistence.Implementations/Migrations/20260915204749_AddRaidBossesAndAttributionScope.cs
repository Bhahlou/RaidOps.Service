using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidBossesAndAttributionScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RaidBossId",
                table: "GuildAttributionDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RaidBosses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RaidZoneId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidBosses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidBosses_RaidZones_RaidZoneId",
                        column: x => x.RaidZoneId,
                        principalTable: "RaidZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "RaidBosses",
                columns: new[] { "Id", "IconUrl", "Name", "RaidZoneId", "SortOrder" },
                values: new object[,]
                {
                    { 1, null, "Attumen the Huntsman", 1, 1 },
                    { 2, null, "Moroes", 1, 2 },
                    { 3, null, "Maiden of Virtue", 1, 3 },
                    { 4, null, "The Opera Event", 1, 4 },
                    { 5, null, "The Curator", 1, 5 },
                    { 6, null, "Terestian Illhoof", 1, 6 },
                    { 7, null, "Shade of Aran", 1, 7 },
                    { 8, null, "Netherspite", 1, 8 },
                    { 9, null, "Chess Event", 1, 9 },
                    { 10, null, "Prince Malchezaar", 1, 10 },
                    { 11, null, "High King Maulgar", 2, 1 },
                    { 12, null, "Gruul the Dragonkiller", 2, 2 },
                    { 13, null, "Magtheridon", 3, 1 },
                    { 14, null, "Hydross the Unstable", 4, 1 },
                    { 15, null, "The Lurker Below", 4, 2 },
                    { 16, null, "Leotheras the Blind", 4, 3 },
                    { 17, null, "Fathom-Lord Karathress", 4, 4 },
                    { 18, null, "Morogrim Tidewalker", 4, 5 },
                    { 19, null, "Lady Vashj", 4, 6 },
                    { 20, null, "Al'ar", 5, 1 },
                    { 21, null, "Void Reaver", 5, 2 },
                    { 22, null, "High Astromancer Solarian", 5, 3 },
                    { 23, null, "Kael'thas Sunstrider", 5, 4 },
                    { 24, null, "Rage Winterchill", 6, 1 },
                    { 25, null, "Anetheron", 6, 2 },
                    { 26, null, "Kaz'rogal", 6, 3 },
                    { 27, null, "Azgalor", 6, 4 },
                    { 28, null, "Archimonde", 6, 5 },
                    { 29, null, "High Warlord Naj'entus", 7, 1 },
                    { 30, null, "Supremus", 7, 2 },
                    { 31, null, "Shade of Akama", 7, 3 },
                    { 32, null, "Teron Gorefiend", 7, 4 },
                    { 33, null, "Gurtogg Bloodboil", 7, 5 },
                    { 34, null, "Reliquary of Souls", 7, 6 },
                    { 35, null, "Mother Shahraz", 7, 7 },
                    { 36, null, "The Illidari Council", 7, 8 },
                    { 37, null, "Illidan Stormrage", 7, 9 },
                    { 38, null, "Kalecgos", 8, 1 },
                    { 39, null, "Brutallus", 8, 2 },
                    { 40, null, "Felmyst", 8, 3 },
                    { 41, null, "Eredar Twins", 8, 4 },
                    { 42, null, "M'uru", 8, 5 },
                    { 43, null, "Kil'jaeden", 8, 6 }
                });

            migrationBuilder.InsertData(
                table: "RaidZones",
                columns: new[] { "Id", "ExpansionId", "GroupCount", "IconUrl", "LockoutAnchorUtc", "LockoutCadenceDays", "Name", "ShortCode", "SlotsPerGroup", "SortOrder" },
                values: new object[] { 9, 2, 2, null, null, null, "Zul'Aman", "ZA", 5, 9 });

            migrationBuilder.InsertData(
                table: "RaidBosses",
                columns: new[] { "Id", "IconUrl", "Name", "RaidZoneId", "SortOrder" },
                values: new object[,]
                {
                    { 44, null, "Akil'zon", 9, 1 },
                    { 45, null, "Nalorakk", 9, 2 },
                    { 46, null, "Jan'alai", 9, 3 },
                    { 47, null, "Halazzi", 9, 4 },
                    { 48, null, "Hex Lord Malacrass", 9, 5 },
                    { 49, null, "Zul'jin", 9, 6 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildAttributionDefinitions_RaidBossId",
                table: "GuildAttributionDefinitions",
                column: "RaidBossId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidBosses_RaidZoneId_SortOrder",
                table: "RaidBosses",
                columns: new[] { "RaidZoneId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_GuildAttributionDefinitions_RaidBosses_RaidBossId",
                table: "GuildAttributionDefinitions",
                column: "RaidBossId",
                principalTable: "RaidBosses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildAttributionDefinitions_RaidBosses_RaidBossId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DropTable(
                name: "RaidBosses");

            migrationBuilder.DropIndex(
                name: "IX_GuildAttributionDefinitions_RaidBossId",
                table: "GuildAttributionDefinitions");

            migrationBuilder.DeleteData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DropColumn(
                name: "RaidBossId",
                table: "GuildAttributionDefinitions");
        }
    }
}
