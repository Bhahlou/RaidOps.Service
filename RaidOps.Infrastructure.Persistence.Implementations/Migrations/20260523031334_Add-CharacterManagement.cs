using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattleNetAccounts",
                columns: table => new
                {
                    UserDiscordId = table.Column<string>(type: "text", nullable: false),
                    BnetId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BattleTag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    TokenExpiry = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Region = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleNetAccounts", x => x.UserDiscordId);
                    table.ForeignKey(
                        name: "FK_BattleNetAccounts_Users_UserDiscordId",
                        column: x => x.UserDiscordId,
                        principalTable: "Users",
                        principalColumn: "DiscordId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Expansions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReleaseOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expansions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Races",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Faction = table.Column<int>(type: "integer", nullable: false),
                    FirstExpansionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Races", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WowClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Color = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    FirstExpansionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WowClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BnetNamespacePrefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentExpansionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Branches_Expansions_CurrentExpansionId",
                        column: x => x.CurrentExpansionId,
                        principalTable: "Expansions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Specs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    FirstExpansionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Specs_WowClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "WowClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Realms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Region = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Realms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Realms_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Faction = table.Column<int>(type: "integer", nullable: false),
                    BnetCharacterId = table.Column<long>(type: "bigint", nullable: false),
                    UserDiscordId = table.Column<string>(type: "text", nullable: false),
                    RealmId = table.Column<int>(type: "integer", nullable: false),
                    RaceId = table.Column<int>(type: "integer", nullable: false),
                    ClassId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_Races_RaceId",
                        column: x => x.RaceId,
                        principalTable: "Races",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Characters_Realms_RealmId",
                        column: x => x.RealmId,
                        principalTable: "Realms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Characters_Users_UserDiscordId",
                        column: x => x.UserDiscordId,
                        principalTable: "Users",
                        principalColumn: "DiscordId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Characters_WowClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "WowClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterExpansionStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CharacterId = table.Column<int>(type: "integer", nullable: false),
                    ExpansionId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ItemLevel = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterExpansionStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterExpansionStates_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterExpansionStates_Expansions_ExpansionId",
                        column: x => x.ExpansionId,
                        principalTable: "Expansions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSpecs",
                columns: table => new
                {
                    CharacterExpansionStateId = table.Column<int>(type: "integer", nullable: false),
                    SpecId = table.Column<int>(type: "integer", nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSpecs", x => new { x.CharacterExpansionStateId, x.SpecId });
                    table.ForeignKey(
                        name: "FK_CharacterSpecs_CharacterExpansionStates_CharacterExpansionS~",
                        column: x => x.CharacterExpansionStateId,
                        principalTable: "CharacterExpansionStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSpecs_Specs_SpecId",
                        column: x => x.SpecId,
                        principalTable: "Specs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Expansions",
                columns: new[] { "Id", "Name", "ReleaseOrder", "ShortCode" },
                values: new object[,]
                {
                    { 1, "Classic", 1, "Classic" },
                    { 2, "The Burning Crusade", 2, "TBC" },
                    { 3, "Wrath of the Lich King", 3, "WotLK" },
                    { 4, "Cataclysm", 4, "Cata" },
                    { 5, "Mists of Pandaria", 5, "MoP" },
                    { 6, "Warlords of Draenor", 6, "WoD" },
                    { 7, "Legion", 7, "Legion" },
                    { 8, "Battle for Azeroth", 8, "BfA" },
                    { 9, "Shadowlands", 9, "SL" },
                    { 10, "Dragonflight", 10, "DF" },
                    { 11, "The War Within", 11, "TWW" }
                });

            migrationBuilder.InsertData(
                table: "Races",
                columns: new[] { "Id", "Faction", "FirstExpansionId", "Name" },
                values: new object[,]
                {
                    { 1, 1, 1, "Human" },
                    { 2, 2, 1, "Orc" },
                    { 3, 1, 1, "Dwarf" },
                    { 4, 1, 1, "Night Elf" },
                    { 5, 2, 1, "Undead" },
                    { 6, 2, 1, "Tauren" },
                    { 7, 1, 1, "Gnome" },
                    { 8, 2, 1, "Troll" },
                    { 9, 2, 4, "Goblin" },
                    { 10, 2, 2, "Blood Elf" },
                    { 11, 1, 2, "Draenei" },
                    { 22, 1, 4, "Worgen" },
                    { 24, 3, 5, "Pandaren" },
                    { 27, 2, 8, "Nightborne" },
                    { 28, 2, 8, "Highmountain Tauren" },
                    { 29, 1, 8, "Void Elf" },
                    { 30, 1, 8, "Lightforged Draenei" },
                    { 31, 2, 8, "Zandalari Troll" },
                    { 32, 2, 8, "Mag'har Orc" },
                    { 34, 1, 8, "Dark Iron Dwarf" },
                    { 35, 2, 8, "Vulpera" },
                    { 36, 1, 8, "Kul Tiran" },
                    { 37, 1, 8, "Mechagnome" },
                    { 52, 1, 10, "Dracthyr (Alliance)" },
                    { 70, 2, 10, "Dracthyr (Horde)" },
                    { 84, 1, 11, "Earthen (Alliance)" },
                    { 85, 2, 11, "Earthen (Horde)" }
                });

            migrationBuilder.InsertData(
                table: "WowClasses",
                columns: new[] { "Id", "Color", "FirstExpansionId", "Name" },
                values: new object[,]
                {
                    { 1, "C79C6E", 1, "Warrior" },
                    { 2, "F58CBA", 1, "Paladin" },
                    { 3, "ABD473", 1, "Hunter" },
                    { 4, "FFF569", 1, "Rogue" },
                    { 5, "FFFFFF", 1, "Priest" },
                    { 6, "C41F3B", 3, "Death Knight" },
                    { 7, "0070DE", 1, "Shaman" },
                    { 8, "69CCF0", 1, "Mage" },
                    { 9, "9482C9", 1, "Warlock" },
                    { 10, "00FF96", 5, "Monk" },
                    { 11, "FF7D0A", 1, "Druid" },
                    { 12, "A330C9", 7, "Demon Hunter" },
                    { 13, "33937F", 10, "Evoker" }
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "BnetNamespacePrefix", "CurrentExpansionId", "Name" },
                values: new object[,]
                {
                    { 1, "dynamic", 11, "Retail" },
                    { 2, "dynamic-classic1x", 1, "Classic Era" },
                    { 3, "dynamic-classic", 5, "MoP Classic" },
                    { 4, "dynamic-classicann", 2, "BC Classic (Anniv.)" }
                });

            migrationBuilder.InsertData(
                table: "Specs",
                columns: new[] { "Id", "ClassId", "FirstExpansionId", "Name", "Role" },
                values: new object[,]
                {
                    { 62, 8, 1, "Arcane", 3 },
                    { 63, 8, 1, "Fire", 3 },
                    { 64, 8, 1, "Frost", 3 },
                    { 65, 2, 1, "Holy", 2 },
                    { 66, 2, 1, "Protection", 1 },
                    { 70, 2, 1, "Retribution", 3 },
                    { 71, 1, 1, "Arms", 3 },
                    { 72, 1, 1, "Fury", 3 },
                    { 73, 1, 1, "Protection", 1 },
                    { 102, 11, 1, "Balance", 3 },
                    { 103, 11, 1, "Feral", 3 },
                    { 104, 11, 5, "Guardian", 1 },
                    { 105, 11, 1, "Restoration", 2 },
                    { 250, 6, 3, "Blood", 1 },
                    { 251, 6, 3, "Frost", 3 },
                    { 252, 6, 3, "Unholy", 3 },
                    { 253, 3, 1, "Beast Mastery", 3 },
                    { 254, 3, 1, "Marksmanship", 3 },
                    { 255, 3, 1, "Survival", 3 },
                    { 256, 5, 1, "Discipline", 2 },
                    { 257, 5, 1, "Holy", 2 },
                    { 258, 5, 1, "Shadow", 3 },
                    { 259, 4, 1, "Assassination", 3 },
                    { 260, 4, 1, "Outlaw", 3 },
                    { 261, 4, 1, "Subtlety", 3 },
                    { 262, 7, 1, "Elemental", 3 },
                    { 263, 7, 1, "Enhancement", 3 },
                    { 264, 7, 1, "Restoration", 2 },
                    { 265, 9, 1, "Affliction", 3 },
                    { 266, 9, 1, "Demonology", 3 },
                    { 267, 9, 1, "Destruction", 3 },
                    { 268, 10, 5, "Brewmaster", 1 },
                    { 269, 10, 5, "Windwalker", 3 },
                    { 270, 10, 5, "Mistweaver", 2 },
                    { 577, 12, 7, "Havoc", 3 },
                    { 581, 12, 7, "Vengeance", 1 },
                    { 1467, 13, 10, "Devastation", 3 },
                    { 1468, 13, 10, "Preservation", 2 },
                    { 1473, 13, 10, "Augmentation", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_CurrentExpansionId",
                table: "Branches",
                column: "CurrentExpansionId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterExpansionStates_CharacterId_ExpansionId",
                table: "CharacterExpansionStates",
                columns: new[] { "CharacterId", "ExpansionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterExpansionStates_ExpansionId",
                table: "CharacterExpansionStates",
                column: "ExpansionId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_BnetCharacterId_RealmId",
                table: "Characters",
                columns: new[] { "BnetCharacterId", "RealmId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_ClassId",
                table: "Characters",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_RaceId",
                table: "Characters",
                column: "RaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_RealmId",
                table: "Characters",
                column: "RealmId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_UserDiscordId",
                table: "Characters",
                column: "UserDiscordId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpecs_SpecId",
                table: "CharacterSpecs",
                column: "SpecId");

            migrationBuilder.CreateIndex(
                name: "IX_Realms_BranchId",
                table: "Realms",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Realms_Slug_Region_BranchId",
                table: "Realms",
                columns: new[] { "Slug", "Region", "BranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specs_ClassId",
                table: "Specs",
                column: "ClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleNetAccounts");

            migrationBuilder.DropTable(
                name: "CharacterSpecs");

            migrationBuilder.DropTable(
                name: "CharacterExpansionStates");

            migrationBuilder.DropTable(
                name: "Specs");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Races");

            migrationBuilder.DropTable(
                name: "Realms");

            migrationBuilder.DropTable(
                name: "WowClasses");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Expansions");
        }
    }
}
