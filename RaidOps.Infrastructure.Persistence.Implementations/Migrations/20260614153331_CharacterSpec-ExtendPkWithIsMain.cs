using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class CharacterSpecExtendPkWithIsMain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CharacterSpecs",
                table: "CharacterSpecs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CharacterSpecs",
                table: "CharacterSpecs",
                columns: new[] { "CharacterExpansionStateId", "SpecId", "IsMain" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CharacterSpecs",
                table: "CharacterSpecs");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CharacterSpecs",
                table: "CharacterSpecs",
                columns: new[] { "CharacterExpansionStateId", "SpecId" });
        }
    }
}
