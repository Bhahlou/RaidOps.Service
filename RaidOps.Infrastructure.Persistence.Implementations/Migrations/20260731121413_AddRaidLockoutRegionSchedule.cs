using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidLockoutRegionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockoutAnchorDate",
                table: "RaidZones");

            migrationBuilder.DropColumn(
                name: "LockoutAnchorDate",
                table: "GuildRaidZoneLockouts");

            migrationBuilder.AlterColumn<int>(
                name: "LockoutCadenceDays",
                table: "RaidZones",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutAnchorUtc",
                table: "RaidZones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutAnchorUtc",
                table: "GuildRaidZoneLockouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "GuildBranches",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WeeklyLockoutSchedules",
                columns: table => new
                {
                    Region = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    AnchorUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CadenceDays = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyLockoutSchedules", x => x.Region);
                });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LockoutAnchorUtc", "LockoutCadenceDays" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "LockoutAnchorUtc", "LockoutCadenceDays" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "LockoutAnchorUtc", "LockoutCadenceDays" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "LockoutAnchorUtc", "LockoutCadenceDays" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "LockoutAnchorUtc", "LockoutCadenceDays" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "LockoutAnchorUtc", "LockoutCadenceDays" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "LockoutAnchorUtc", "LockoutCadenceDays" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "LockoutAnchorUtc", "LockoutCadenceDays" },
                values: new object[] { null, null });

            migrationBuilder.InsertData(
                table: "WeeklyLockoutSchedules",
                columns: new[] { "Region", "AnchorUtc", "CadenceDays" },
                values: new object[,]
                {
                    { "eu", new DateTime(2023, 1, 4, 4, 0, 0, 0, DateTimeKind.Utc), 7 },
                    { "us", new DateTime(2023, 1, 3, 15, 0, 0, 0, DateTimeKind.Utc), 7 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyLockoutSchedules");

            migrationBuilder.DropColumn(
                name: "LockoutAnchorUtc",
                table: "RaidZones");

            migrationBuilder.DropColumn(
                name: "LockoutAnchorUtc",
                table: "GuildRaidZoneLockouts");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "GuildBranches");

            migrationBuilder.AlterColumn<int>(
                name: "LockoutCadenceDays",
                table: "RaidZones",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LockoutAnchorDate",
                table: "RaidZones",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "LockoutAnchorDate",
                table: "GuildRaidZoneLockouts",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "LockoutAnchorDate", "LockoutCadenceDays" },
                values: new object[] { new DateOnly(2007, 1, 16), 7 });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "LockoutAnchorDate", "LockoutCadenceDays" },
                values: new object[] { new DateOnly(2007, 1, 16), 7 });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "LockoutAnchorDate", "LockoutCadenceDays" },
                values: new object[] { new DateOnly(2007, 1, 16), 7 });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "LockoutAnchorDate", "LockoutCadenceDays" },
                values: new object[] { new DateOnly(2007, 1, 16), 7 });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "LockoutAnchorDate", "LockoutCadenceDays" },
                values: new object[] { new DateOnly(2007, 1, 16), 7 });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "LockoutAnchorDate", "LockoutCadenceDays" },
                values: new object[] { new DateOnly(2007, 1, 16), 7 });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "LockoutAnchorDate", "LockoutCadenceDays" },
                values: new object[] { new DateOnly(2007, 1, 16), 7 });

            migrationBuilder.UpdateData(
                table: "RaidZones",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "LockoutAnchorDate", "LockoutCadenceDays" },
                values: new object[] { new DateOnly(2007, 1, 16), 7 });
        }
    }
}
