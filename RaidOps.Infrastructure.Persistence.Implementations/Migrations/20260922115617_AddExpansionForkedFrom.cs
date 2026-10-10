using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddExpansionForkedFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ForkedFromExpansionId",
                table: "Expansions",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 1,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 2,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 3,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 4,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 5,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 6,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 7,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 8,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 9,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 10,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 11,
                column: "ForkedFromExpansionId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Expansions",
                keyColumn: "Id",
                keyValue: 12,
                column: "ForkedFromExpansionId",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Expansions_ForkedFromExpansionId",
                table: "Expansions",
                column: "ForkedFromExpansionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Expansions_Expansions_ForkedFromExpansionId",
                table: "Expansions",
                column: "ForkedFromExpansionId",
                principalTable: "Expansions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expansions_Expansions_ForkedFromExpansionId",
                table: "Expansions");

            migrationBuilder.DropIndex(
                name: "IX_Expansions_ForkedFromExpansionId",
                table: "Expansions");

            migrationBuilder.DropColumn(
                name: "ForkedFromExpansionId",
                table: "Expansions");
        }
    }
}
