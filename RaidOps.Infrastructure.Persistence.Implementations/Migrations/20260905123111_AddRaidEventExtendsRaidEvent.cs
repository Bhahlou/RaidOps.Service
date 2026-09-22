using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidEventExtendsRaidEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExtendsRaidEventId",
                table: "RaidEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidEvents_ExtendsRaidEventId",
                table: "RaidEvents",
                column: "ExtendsRaidEventId");

            migrationBuilder.AddForeignKey(
                name: "FK_RaidEvents_RaidEvents_ExtendsRaidEventId",
                table: "RaidEvents",
                column: "ExtendsRaidEventId",
                principalTable: "RaidEvents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RaidEvents_RaidEvents_ExtendsRaidEventId",
                table: "RaidEvents");

            migrationBuilder.DropIndex(
                name: "IX_RaidEvents_ExtendsRaidEventId",
                table: "RaidEvents");

            migrationBuilder.DropColumn(
                name: "ExtendsRaidEventId",
                table: "RaidEvents");
        }
    }
}
