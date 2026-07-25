using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaidSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceDayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    RecurrenceStartTimeLocal = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    RecurrenceIntervalWeeks = table.Column<int>(type: "integer", nullable: false),
                    GroupCount = table.Column<int>(type: "integer", nullable: false),
                    SlotsPerGroup = table.Column<int>(type: "integer", nullable: false),
                    SignupMode = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByDiscordId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidSeries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaidSeries_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaidZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExpansionId = table.Column<int>(type: "integer", nullable: false),
                    GroupCount = table.Column<int>(type: "integer", nullable: false),
                    SlotsPerGroup = table.Column<int>(type: "integer", nullable: false),
                    LockoutCadenceDays = table.Column<int>(type: "integer", nullable: false),
                    LockoutAnchorDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidZones_Expansions_ExpansionId",
                        column: x => x.ExpansionId,
                        principalTable: "Expansions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaidEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    RaidSeriesId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GroupCount = table.Column<int>(type: "integer", nullable: false),
                    SlotsPerGroup = table.Column<int>(type: "integer", nullable: false),
                    SignupMode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublicationStatus = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedByDiscordId = table.Column<string>(type: "text", nullable: true),
                    CreatedByDiscordId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidEvents_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaidEvents_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaidEvents_RaidSeries_RaidSeriesId",
                        column: x => x.RaidSeriesId,
                        principalTable: "RaidSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GuildRaidZoneLockouts",
                columns: table => new
                {
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    RaidZoneId = table.Column<int>(type: "integer", nullable: false),
                    LockoutAnchorDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LockoutCadenceDays = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildRaidZoneLockouts", x => new { x.GuildId, x.RaidZoneId });
                    table.ForeignKey(
                        name: "FK_GuildRaidZoneLockouts_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuildRaidZoneLockouts_RaidZones_RaidZoneId",
                        column: x => x.RaidZoneId,
                        principalTable: "RaidZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaidLockoutCadenceOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RaidZoneId = table.Column<int>(type: "integer", nullable: false),
                    CadenceDays = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedByDiscordId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidLockoutCadenceOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidLockoutCadenceOverrides_RaidZones_RaidZoneId",
                        column: x => x.RaidZoneId,
                        principalTable: "RaidZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RaidSeriesZones",
                columns: table => new
                {
                    RaidSeriesId = table.Column<int>(type: "integer", nullable: false),
                    RaidZoneId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidSeriesZones", x => new { x.RaidSeriesId, x.RaidZoneId });
                    table.ForeignKey(
                        name: "FK_RaidSeriesZones_RaidSeries_RaidSeriesId",
                        column: x => x.RaidSeriesId,
                        principalTable: "RaidSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaidSeriesZones_RaidZones_RaidZoneId",
                        column: x => x.RaidZoneId,
                        principalTable: "RaidZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaidEventZones",
                columns: table => new
                {
                    RaidEventId = table.Column<int>(type: "integer", nullable: false),
                    RaidZoneId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidEventZones", x => new { x.RaidEventId, x.RaidZoneId });
                    table.ForeignKey(
                        name: "FK_RaidEventZones_RaidEvents_RaidEventId",
                        column: x => x.RaidEventId,
                        principalTable: "RaidEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaidEventZones_RaidZones_RaidZoneId",
                        column: x => x.RaidZoneId,
                        principalTable: "RaidZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaidSlotAssignments",
                columns: table => new
                {
                    RaidEventId = table.Column<int>(type: "integer", nullable: false),
                    GroupNumber = table.Column<int>(type: "integer", nullable: false),
                    SlotNumber = table.Column<int>(type: "integer", nullable: false),
                    CharacterId = table.Column<int>(type: "integer", nullable: false),
                    AssignedPlayerDiscordId = table.Column<string>(type: "text", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedByDiscordId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidSlotAssignments", x => new { x.RaidEventId, x.GroupNumber, x.SlotNumber });
                    table.ForeignKey(
                        name: "FK_RaidSlotAssignments_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaidSlotAssignments_RaidEvents_RaidEventId",
                        column: x => x.RaidEventId,
                        principalTable: "RaidEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "RaidZones",
                columns: new[] { "Id", "ExpansionId", "GroupCount", "IconUrl", "LockoutAnchorDate", "LockoutCadenceDays", "Name", "ShortCode", "SlotsPerGroup", "SortOrder" },
                values: new object[,]
                {
                    { 1, 2, 2, null, new DateOnly(2007, 1, 16), 7, "Karazhan", "Kara", 5, 1 },
                    { 2, 2, 5, null, new DateOnly(2007, 1, 16), 7, "Gruul's Lair", "Gruul", 5, 2 },
                    { 3, 2, 5, null, new DateOnly(2007, 1, 16), 7, "Magtheridon's Lair", "Mag", 5, 3 },
                    { 4, 2, 5, null, new DateOnly(2007, 1, 16), 7, "Serpentshrine Cavern", "SSC", 5, 4 },
                    { 5, 2, 5, null, new DateOnly(2007, 1, 16), 7, "The Eye", "TK", 5, 5 },
                    { 6, 2, 5, null, new DateOnly(2007, 1, 16), 7, "Mount Hyjal", "Hyjal", 5, 6 },
                    { 7, 2, 5, null, new DateOnly(2007, 1, 16), 7, "Black Temple", "BT", 5, 7 },
                    { 8, 2, 5, null, new DateOnly(2007, 1, 16), 7, "Sunwell Plateau", "SWP", 5, 8 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildRaidZoneLockouts_RaidZoneId",
                table: "GuildRaidZoneLockouts",
                column: "RaidZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidEvents_BranchId",
                table: "RaidEvents",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidEvents_GuildId_StartsAtUtc",
                table: "RaidEvents",
                columns: new[] { "GuildId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RaidEvents_RaidSeriesId",
                table: "RaidEvents",
                column: "RaidSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidEventZones_RaidZoneId",
                table: "RaidEventZones",
                column: "RaidZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidLockoutCadenceOverrides_RaidZoneId_EffectiveFrom",
                table: "RaidLockoutCadenceOverrides",
                columns: new[] { "RaidZoneId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_RaidSeries_BranchId",
                table: "RaidSeries",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidSeries_GuildId",
                table: "RaidSeries",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidSeriesZones_RaidZoneId",
                table: "RaidSeriesZones",
                column: "RaidZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidSlotAssignments_CharacterId",
                table: "RaidSlotAssignments",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidSlotAssignments_RaidEventId_AssignedPlayerDiscordId",
                table: "RaidSlotAssignments",
                columns: new[] { "RaidEventId", "AssignedPlayerDiscordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidSlotAssignments_RaidEventId_CharacterId",
                table: "RaidSlotAssignments",
                columns: new[] { "RaidEventId", "CharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidZones_ExpansionId",
                table: "RaidZones",
                column: "ExpansionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildRaidZoneLockouts");

            migrationBuilder.DropTable(
                name: "RaidEventZones");

            migrationBuilder.DropTable(
                name: "RaidLockoutCadenceOverrides");

            migrationBuilder.DropTable(
                name: "RaidSeriesZones");

            migrationBuilder.DropTable(
                name: "RaidSlotAssignments");

            migrationBuilder.DropTable(
                name: "RaidZones");

            migrationBuilder.DropTable(
                name: "RaidEvents");

            migrationBuilder.DropTable(
                name: "RaidSeries");
        }
    }
}
