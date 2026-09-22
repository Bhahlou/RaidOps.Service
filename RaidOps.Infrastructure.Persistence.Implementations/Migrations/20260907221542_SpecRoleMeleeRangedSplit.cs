using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class SpecRoleMeleeRangedSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 70,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 71,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 72,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 103,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 251,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 252,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 255,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 259,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 260,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 261,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 263,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 269,
                column: "Role",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 577,
                column: "Role",
                value: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 70,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 71,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 72,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 103,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 251,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 252,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 255,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 259,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 260,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 261,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 263,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 269,
                column: "Role",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 577,
                column: "Role",
                value: 3);
        }
    }
}
