using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToCharacter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Characters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_BranchId",
                table: "Characters",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Branches_BranchId",
                table: "Characters",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Branches_BranchId",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_BranchId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Characters");
        }
    }
}
