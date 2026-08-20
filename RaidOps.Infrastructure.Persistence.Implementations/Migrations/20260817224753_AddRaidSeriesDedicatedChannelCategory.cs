using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidSeriesDedicatedChannelCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedicatedAnnouncementChannelCategoryId",
                table: "RaidSeries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DedicatedAnnouncementChannelCategoryId",
                table: "RaidSeries");
        }
    }
}
