using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidSignupSpecId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SpecId",
                table: "RaidSignups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidSignups_SpecId",
                table: "RaidSignups",
                column: "SpecId");

            migrationBuilder.AddForeignKey(
                name: "FK_RaidSignups_Specs_SpecId",
                table: "RaidSignups",
                column: "SpecId",
                principalTable: "Specs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidSignups_Specs_SpecId",
                table: "RaidSignups");

            migrationBuilder.DropIndex(
                name: "IX_RaidSignups_SpecId",
                table: "RaidSignups");

            migrationBuilder.DropColumn(
                name: "SpecId",
                table: "RaidSignups");
        }
    }
}
