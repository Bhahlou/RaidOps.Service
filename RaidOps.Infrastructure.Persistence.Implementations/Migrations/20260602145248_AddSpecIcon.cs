using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                table: "Specs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 62,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 63,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 64,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 65,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 66,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 70,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 71,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 72,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 73,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 102,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 103,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 104,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 105,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 250,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 251,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 252,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 253,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 254,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 255,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 256,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 257,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 258,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 259,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 260,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 261,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 262,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 263,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 264,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 265,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 266,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 267,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 268,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 269,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 270,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 577,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 581,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 1467,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 1468,
                column: "IconUrl",
                value: null);

            migrationBuilder.UpdateData(
                table: "Specs",
                keyColumn: "Id",
                keyValue: 1473,
                column: "IconUrl",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconUrl",
                table: "Specs");
        }
    }
}
