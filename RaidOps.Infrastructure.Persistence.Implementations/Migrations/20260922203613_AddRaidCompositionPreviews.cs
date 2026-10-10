using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidCompositionPreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaidCompositionPreviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildBranchId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: false),
                    GroupCount = table.Column<int>(type: "integer", nullable: false),
                    SlotsPerGroup = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByDiscordId = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidCompositionPreviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidCompositionPreviews_GuildBranches_GuildBranchId",
                        column: x => x.GuildBranchId,
                        principalTable: "GuildBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RaidCompositionPreviewSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RaidCompositionPreviewId = table.Column<int>(type: "integer", nullable: false),
                    GroupNumber = table.Column<int>(type: "integer", nullable: false),
                    SlotNumber = table.Column<int>(type: "integer", nullable: false),
                    WowClassId = table.Column<int>(type: "integer", nullable: true),
                    SpecId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidCompositionPreviewSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidCompositionPreviewSlots_RaidCompositionPreviews_RaidCom~",
                        column: x => x.RaidCompositionPreviewId,
                        principalTable: "RaidCompositionPreviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaidCompositionPreviewSlots_Specs_SpecId",
                        column: x => x.SpecId,
                        principalTable: "Specs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaidCompositionPreviewSlots_WowClasses_WowClassId",
                        column: x => x.WowClassId,
                        principalTable: "WowClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaidCompositionPreviews_GuildBranchId",
                table: "RaidCompositionPreviews",
                column: "GuildBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidCompositionPreviewSlots_RaidCompositionPreviewId_GroupN~",
                table: "RaidCompositionPreviewSlots",
                columns: new[] { "RaidCompositionPreviewId", "GroupNumber", "SlotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidCompositionPreviewSlots_SpecId",
                table: "RaidCompositionPreviewSlots",
                column: "SpecId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidCompositionPreviewSlots_WowClassId",
                table: "RaidCompositionPreviewSlots",
                column: "WowClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaidCompositionPreviewSlots");

            migrationBuilder.DropTable(
                name: "RaidCompositionPreviews");
        }
    }
}
