using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvailabilityExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserDiscordId = table.Column<string>(type: "text", nullable: false),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AvailableFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    AvailableUntil = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailabilityExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvailabilityExceptions_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvailabilityExceptions_Users_UserDiscordId",
                        column: x => x.UserDiscordId,
                        principalTable: "Users",
                        principalColumn: "DiscordId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringAvailabilityPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserDiscordId = table.Column<string>(type: "text", nullable: false),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CycleLengthDays = table.Column<int>(type: "integer", nullable: false),
                    AnchorDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveUntil = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringAvailabilityPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringAvailabilityPatterns_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringAvailabilityPatterns_Users_UserDiscordId",
                        column: x => x.UserDiscordId,
                        principalTable: "Users",
                        principalColumn: "DiscordId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringAvailabilityPatternDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatternId = table.Column<int>(type: "integer", nullable: false),
                    OffsetInCycle = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AvailableFrom = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    AvailableUntil = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringAvailabilityPatternDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringAvailabilityPatternDays_RecurringAvailabilityPatte~",
                        column: x => x.PatternId,
                        principalTable: "RecurringAvailabilityPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityExceptions_GuildId",
                table: "AvailabilityExceptions",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityExceptions_UserDiscordId_GuildId_StartDate_EndD~",
                table: "AvailabilityExceptions",
                columns: new[] { "UserDiscordId", "GuildId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringAvailabilityPatternDays_PatternId_OffsetInCycle",
                table: "RecurringAvailabilityPatternDays",
                columns: new[] { "PatternId", "OffsetInCycle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringAvailabilityPatterns_GuildId",
                table: "RecurringAvailabilityPatterns",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringAvailabilityPatterns_UserDiscordId_GuildId",
                table: "RecurringAvailabilityPatterns",
                columns: new[] { "UserDiscordId", "GuildId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvailabilityExceptions");

            migrationBuilder.DropTable(
                name: "RecurringAvailabilityPatternDays");

            migrationBuilder.DropTable(
                name: "RecurringAvailabilityPatterns");
        }
    }
}
