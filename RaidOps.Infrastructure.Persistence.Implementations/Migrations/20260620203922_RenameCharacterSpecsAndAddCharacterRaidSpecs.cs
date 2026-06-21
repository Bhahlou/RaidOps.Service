using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class RenameCharacterSpecsAndAddCharacterRaidSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renamed in place (not drop+recreate) to preserve existing BNet spec data.
            migrationBuilder.RenameTable(
                name: "CharacterSpecs",
                newName: "BnetCharacterSpecs");

            migrationBuilder.RenameIndex(
                name: "IX_CharacterSpecs_SpecId",
                table: "BnetCharacterSpecs",
                newName: "IX_BnetCharacterSpecs_SpecId");

            migrationBuilder.Sql(
                @"ALTER TABLE ""BnetCharacterSpecs"" RENAME CONSTRAINT ""PK_CharacterSpecs"" TO ""PK_BnetCharacterSpecs"";");
            migrationBuilder.Sql(
                @"ALTER TABLE ""BnetCharacterSpecs"" RENAME CONSTRAINT ""FK_CharacterSpecs_CharacterExpansionStates_CharacterExpansionS~"" TO ""FK_BnetCharacterSpecs_CharacterExpansionStates_CharacterExpans~"";");
            migrationBuilder.Sql(
                @"ALTER TABLE ""BnetCharacterSpecs"" RENAME CONSTRAINT ""FK_CharacterSpecs_Specs_SpecId"" TO ""FK_BnetCharacterSpecs_Specs_SpecId"";");

            migrationBuilder.CreateTable(
                name: "CharacterRaidSpecs",
                columns: table => new
                {
                    CharacterId = table.Column<int>(type: "integer", nullable: false),
                    SpecId = table.Column<int>(type: "integer", nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterRaidSpecs", x => new { x.CharacterId, x.SpecId });
                    table.ForeignKey(
                        name: "FK_CharacterRaidSpecs_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterRaidSpecs_Specs_SpecId",
                        column: x => x.SpecId,
                        principalTable: "Specs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterRaidSpecs_SpecId",
                table: "CharacterRaidSpecs",
                column: "SpecId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterRaidSpecs");

            migrationBuilder.Sql(
                @"ALTER TABLE ""BnetCharacterSpecs"" RENAME CONSTRAINT ""PK_BnetCharacterSpecs"" TO ""PK_CharacterSpecs"";");
            migrationBuilder.Sql(
                @"ALTER TABLE ""BnetCharacterSpecs"" RENAME CONSTRAINT ""FK_BnetCharacterSpecs_CharacterExpansionStates_CharacterExpans~"" TO ""FK_CharacterSpecs_CharacterExpansionStates_CharacterExpansionS~"";");
            migrationBuilder.Sql(
                @"ALTER TABLE ""BnetCharacterSpecs"" RENAME CONSTRAINT ""FK_BnetCharacterSpecs_Specs_SpecId"" TO ""FK_CharacterSpecs_Specs_SpecId"";");

            migrationBuilder.RenameIndex(
                name: "IX_BnetCharacterSpecs_SpecId",
                table: "BnetCharacterSpecs",
                newName: "IX_CharacterSpecs_SpecId");

            migrationBuilder.RenameTable(
                name: "BnetCharacterSpecs",
                newName: "CharacterSpecs");
        }
    }
}
