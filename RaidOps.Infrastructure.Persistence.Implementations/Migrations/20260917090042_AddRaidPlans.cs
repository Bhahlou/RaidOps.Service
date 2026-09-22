using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaidPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    RaidBossId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByDiscordId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidPlans_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaidPlans_RaidBosses_RaidBossId",
                        column: x => x.RaidBossId,
                        principalTable: "RaidBosses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaidPlanPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RaidPlanId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    BackgroundImageKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidPlanPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidPlanPages_RaidPlans_RaidPlanId",
                        column: x => x.RaidPlanId,
                        principalTable: "RaidPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RaidPlanElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RaidPlanPageId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ZIndex = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    Width = table.Column<double>(type: "double precision", nullable: false),
                    Height = table.Column<double>(type: "double precision", nullable: false),
                    RotationDegrees = table.Column<double>(type: "double precision", nullable: false),
                    IconSource = table.Column<int>(type: "integer", nullable: false),
                    SpellId = table.Column<int>(type: "integer", nullable: true),
                    RaidMarker = table.Column<int>(type: "integer", nullable: true),
                    StaticRole = table.Column<int>(type: "integer", nullable: true),
                    Text = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FontSizeRatio = table.Column<double>(type: "double precision", nullable: true),
                    StrokeColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    FillColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    StrokeWidthRatio = table.Column<double>(type: "double precision", nullable: true),
                    X2 = table.Column<double>(type: "double precision", nullable: true),
                    Y2 = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidPlanElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidPlanElements_RaidPlanPages_RaidPlanPageId",
                        column: x => x.RaidPlanPageId,
                        principalTable: "RaidPlanPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaidPlanElements_Spells_SpellId",
                        column: x => x.SpellId,
                        principalTable: "Spells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaidPlanElements_RaidPlanPageId_ZIndex",
                table: "RaidPlanElements",
                columns: new[] { "RaidPlanPageId", "ZIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_RaidPlanElements_SpellId",
                table: "RaidPlanElements",
                column: "SpellId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidPlanPages_RaidPlanId_SortOrder",
                table: "RaidPlanPages",
                columns: new[] { "RaidPlanId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RaidPlans_GuildId_RaidBossId",
                table: "RaidPlans",
                columns: new[] { "GuildId", "RaidBossId" });

            migrationBuilder.CreateIndex(
                name: "IX_RaidPlans_RaidBossId",
                table: "RaidPlans",
                column: "RaidBossId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaidPlanElements");

            migrationBuilder.DropTable(
                name: "RaidPlanPages");

            migrationBuilder.DropTable(
                name: "RaidPlans");
        }
    }
}
