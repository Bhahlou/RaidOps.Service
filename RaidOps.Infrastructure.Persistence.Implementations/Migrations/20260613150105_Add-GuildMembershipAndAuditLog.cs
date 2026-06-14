using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildMembershipAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    ActorDiscordId = table.Column<string>(type: "text", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildAuditLogs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuildMemberships",
                columns: table => new
                {
                    CharacterId = table.Column<int>(type: "integer", nullable: false),
                    GuildId = table.Column<string>(type: "text", nullable: false),
                    CharacterRank = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMemberships", x => new { x.CharacterId, x.GuildId });
                    table.ForeignKey(
                        name: "FK_GuildMemberships_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildMemberships_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildAuditLogs_GuildId_OccurredAt",
                table: "GuildAuditLogs",
                columns: new[] { "GuildId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMemberships_GuildId",
                table: "GuildMemberships",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildAuditLogs");

            migrationBuilder.DropTable(
                name: "GuildMemberships");
        }
    }
}
