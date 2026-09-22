using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddForeverBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Expansions",
                columns: new[] { "Id", "Name", "ReleaseOrder", "ShortCode" },
                values: new object[] { 12, "Forever", 12, "Forever" });

            migrationBuilder.InsertData(
                table: "Races",
                columns: new[] { "Id", "Faction", "FirstExpansionId", "Name" },
                values: new object[,]
                {
                    { 90, 1, 12, "Skyborne (Alliance)" },
                    { 91, 2, 12, "Skyborne (Horde)" }
                });

            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "BnetNamespacePrefix", "CurrentExpansionId", "Name" },
                values: new object[] { 5, "dynamic-forever", 12, "Forever" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Branches",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Races",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "Races",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 12);
        }
    }
}
