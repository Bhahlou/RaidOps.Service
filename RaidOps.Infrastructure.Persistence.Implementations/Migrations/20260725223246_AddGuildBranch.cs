using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. New structures first, old columns stay in place for the backfill to read ──

            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                table: "UserGuilds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GuildBranches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    BranchId = table.Column<int>(type: "integer", nullable: false),
                    RosterMode = table.Column<int>(type: "integer", nullable: true),
                    RosterRoleIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    OfficerRoleIds = table.Column<List<string>>(type: "text[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildBranches_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildBranches_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildBranches_BranchId",
                table: "GuildBranches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildBranches_GuildId_BranchId",
                table: "GuildBranches",
                columns: new[] { "GuildId", "BranchId" },
                unique: true);

            // GuildBranchId starts nullable so the backfill below can populate it row by row —
            // tightened to NOT NULL once every existing membership has been assigned one.
            migrationBuilder.AddColumn<int>(
                name: "GuildBranchId",
                table: "GuildMemberships",
                type: "integer",
                nullable: true);

            // ── 2. Backfill: one GuildBranch per already-configured guild that has at least one
            //    roster member (BranchId inferred by majority vote across that guild's members'
            //    characters), copying its RosterMode/MinRosterRoleId/MinOfficerRoleId. A configured
            //    guild with zero members yet gets no GuildBranch row here — there is no member data
            //    to infer BranchId from, so its owner must pick a branch on next settings visit
            //    before roster features work again (RosterMode/roles were already configured, so
            //    this is a narrow gap, not a full reconfiguration).

            migrationBuilder.Sql(
                """
                INSERT INTO "GuildBranches" ("GuildId", "BranchId", "RosterMode", "RosterRoleIds", "OfficerRoleIds", "IsActive", "CreatedAt")
                SELECT g."Id", branch_votes."BranchId", g."RosterMode",
                       CASE WHEN g."MinRosterRoleId" IS NOT NULL THEN ARRAY[g."MinRosterRoleId"] ELSE ARRAY[]::text[] END,
                       CASE WHEN g."MinOfficerRoleId" IS NOT NULL THEN ARRAY[g."MinOfficerRoleId"] ELSE ARRAY[]::text[] END,
                       true, now()
                FROM "Guilds" g
                CROSS JOIN LATERAL (
                    SELECT c."BranchId", COUNT(*) AS cnt
                    FROM "GuildMemberships" gm
                    JOIN "Characters" c ON c."Id" = gm."CharacterId"
                    WHERE gm."GuildId" = g."Id"
                    GROUP BY c."BranchId"
                    ORDER BY cnt DESC, c."BranchId" ASC
                    LIMIT 1
                ) branch_votes
                WHERE g."RosterMode" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "GuildMemberships" gm
                SET "GuildBranchId" = gb."Id"
                FROM "GuildBranches" gb
                WHERE gb."GuildId" = gm."GuildId";
                """);

            // ── 3. Tighten GuildBranchId now that every existing membership has one, then drop the
            //    old Guild-level columns the backfill just read from ──

            migrationBuilder.AlterColumn<int>(
                name: "GuildBranchId",
                table: "GuildMemberships",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMemberships_GuildBranchId",
                table: "GuildMemberships",
                column: "GuildBranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuildMemberships_GuildBranches_GuildBranchId",
                table: "GuildMemberships",
                column: "GuildBranchId",
                principalTable: "GuildBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "MinOfficerRoleId",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "MinRosterRoleId",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "RosterMode",
                table: "Guilds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuildMemberships_GuildBranches_GuildBranchId",
                table: "GuildMemberships");

            migrationBuilder.DropTable(
                name: "GuildBranches");

            migrationBuilder.DropIndex(
                name: "IX_GuildMemberships_GuildBranchId",
                table: "GuildMemberships");

            migrationBuilder.DropColumn(
                name: "IsOwner",
                table: "UserGuilds");

            migrationBuilder.DropColumn(
                name: "GuildBranchId",
                table: "GuildMemberships");

            migrationBuilder.AddColumn<string>(
                name: "MinOfficerRoleId",
                table: "Guilds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MinRosterRoleId",
                table: "Guilds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RosterMode",
                table: "Guilds",
                type: "integer",
                nullable: true);
        }
    }
}
