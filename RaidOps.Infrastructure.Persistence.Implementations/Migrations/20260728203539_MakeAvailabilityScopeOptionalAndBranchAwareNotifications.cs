using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <summary>
    /// Availability declarations/patterns move from required (UserDiscordId, GuildId) to an optional
    /// scope: Global (GuildId and GuildBranchId both null) or a specific GuildBranch (both set).
    /// Existing rows (~8 days, predating the notion of a branch) are backfilled to Global — there is
    /// no reliable signal to infer which branch they meant, and Global is the closest match to their
    /// original guild-wide-only semantics. GuildNotificationSetting also becomes branch-aware
    /// (nullable GuildBranchId, null = guild-wide fallback row) via a surrogate Id primary key, since
    /// Postgres primary keys cannot contain a nullable column — see the two partial unique indexes
    /// below for how (GuildId, EventType, GuildBranchId) uniqueness is enforced instead.
    /// </summary>
    public partial class MakeAvailabilityScopeOptionalAndBranchAwareNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GuildNotificationSettings",
                table: "GuildNotificationSettings");

            migrationBuilder.AlterColumn<string>(
                name: "GuildId",
                table: "RecurringAvailabilityPatterns",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "GuildBranchId",
                table: "RecurringAvailabilityPatterns",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "GuildNotificationSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "GuildBranchId",
                table: "GuildNotificationSettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuildId",
                table: "AvailabilityExceptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "GuildBranchId",
                table: "AvailabilityExceptions",
                type: "integer",
                nullable: true);

            // Backfill: existing rows predate the notion of a branch, so they become Global rather
            // than guessing a branch. GuildBranchId is already NULL for these rows (just-added
            // nullable column, no default) — this only needs to null out GuildId to match, before
            // the Global-or-branch check constraints below are added.
            migrationBuilder.Sql(
                """UPDATE "AvailabilityExceptions" SET "GuildId" = NULL;""");

            migrationBuilder.Sql(
                """UPDATE "RecurringAvailabilityPatterns" SET "GuildId" = NULL;""");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GuildNotificationSettings",
                table: "GuildNotificationSettings",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringAvailabilityPatterns_GuildBranchId",
                table: "RecurringAvailabilityPatterns",
                column: "GuildBranchId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringAvailabilityPatterns_ScopeBothOrNeither",
                table: "RecurringAvailabilityPatterns",
                sql: "(\"GuildId\" IS NULL AND \"GuildBranchId\" IS NULL) OR (\"GuildId\" IS NOT NULL AND \"GuildBranchId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_GuildNotificationSettings_GuildBranchId",
                table: "GuildNotificationSettings",
                column: "GuildBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildNotificationSettings_GuildId_EventType",
                table: "GuildNotificationSettings",
                columns: new[] { "GuildId", "EventType" },
                unique: true,
                filter: "(\"GuildBranchId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_GuildNotificationSettings_GuildId_EventType_GuildBranchId",
                table: "GuildNotificationSettings",
                columns: new[] { "GuildId", "EventType", "GuildBranchId" },
                unique: true,
                filter: "(\"GuildBranchId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityExceptions_GuildBranchId",
                table: "AvailabilityExceptions",
                column: "GuildBranchId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AvailabilityExceptions_ScopeBothOrNeither",
                table: "AvailabilityExceptions",
                sql: "(\"GuildId\" IS NULL AND \"GuildBranchId\" IS NULL) OR (\"GuildId\" IS NOT NULL AND \"GuildBranchId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_AvailabilityExceptions_GuildBranches_GuildBranchId",
                table: "AvailabilityExceptions",
                column: "GuildBranchId",
                principalTable: "GuildBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GuildNotificationSettings_GuildBranches_GuildBranchId",
                table: "GuildNotificationSettings",
                column: "GuildBranchId",
                principalTable: "GuildBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringAvailabilityPatterns_GuildBranches_GuildBranchId",
                table: "RecurringAvailabilityPatterns",
                column: "GuildBranchId",
                principalTable: "GuildBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AvailabilityExceptions_GuildBranches_GuildBranchId",
                table: "AvailabilityExceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_GuildNotificationSettings_GuildBranches_GuildBranchId",
                table: "GuildNotificationSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringAvailabilityPatterns_GuildBranches_GuildBranchId",
                table: "RecurringAvailabilityPatterns");

            migrationBuilder.DropIndex(
                name: "IX_RecurringAvailabilityPatterns_GuildBranchId",
                table: "RecurringAvailabilityPatterns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringAvailabilityPatterns_ScopeBothOrNeither",
                table: "RecurringAvailabilityPatterns");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GuildNotificationSettings",
                table: "GuildNotificationSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildNotificationSettings_GuildBranchId",
                table: "GuildNotificationSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildNotificationSettings_GuildId_EventType",
                table: "GuildNotificationSettings");

            migrationBuilder.DropIndex(
                name: "IX_GuildNotificationSettings_GuildId_EventType_GuildBranchId",
                table: "GuildNotificationSettings");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilityExceptions_GuildBranchId",
                table: "AvailabilityExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AvailabilityExceptions_ScopeBothOrNeither",
                table: "AvailabilityExceptions");

            migrationBuilder.DropColumn(
                name: "GuildBranchId",
                table: "RecurringAvailabilityPatterns");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "GuildNotificationSettings");

            migrationBuilder.DropColumn(
                name: "GuildBranchId",
                table: "GuildNotificationSettings");

            migrationBuilder.DropColumn(
                name: "GuildBranchId",
                table: "AvailabilityExceptions");

            migrationBuilder.AlterColumn<string>(
                name: "GuildId",
                table: "RecurringAvailabilityPatterns",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuildId",
                table: "AvailabilityExceptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_GuildNotificationSettings",
                table: "GuildNotificationSettings",
                columns: new[] { "GuildId", "EventType" });
        }
    }
}
