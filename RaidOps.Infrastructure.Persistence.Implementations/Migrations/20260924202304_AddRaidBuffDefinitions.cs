using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddRaidBuffDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RaidBuffDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExpansionId = table.Column<int>(type: "integer", nullable: false),
                    SpellId = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    LabelEn = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LabelFr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LabelDe = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExclusiveGroupKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CapacityPoolKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidBuffDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidBuffDefinitions_Expansions_ExpansionId",
                        column: x => x.ExpansionId,
                        principalTable: "Expansions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaidBuffDefinitions_Spells_SpellId",
                        column: x => x.SpellId,
                        principalTable: "Spells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RaidBuffSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RaidBuffDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    ClassId = table.Column<int>(type: "integer", nullable: false),
                    SpecId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidBuffSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RaidBuffSources_RaidBuffDefinitions_RaidBuffDefinitionId",
                        column: x => x.RaidBuffDefinitionId,
                        principalTable: "RaidBuffDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RaidBuffSources_Specs_SpecId",
                        column: x => x.SpecId,
                        principalTable: "Specs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RaidBuffSources_WowClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "WowClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RaidBuffDefinitions_ExpansionId_SpellId",
                table: "RaidBuffDefinitions",
                columns: new[] { "ExpansionId", "SpellId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RaidBuffDefinitions_SpellId",
                table: "RaidBuffDefinitions",
                column: "SpellId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidBuffSources_ClassId",
                table: "RaidBuffSources",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidBuffSources_RaidBuffDefinitionId",
                table: "RaidBuffSources",
                column: "RaidBuffDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidBuffSources_SpecId",
                table: "RaidBuffSources",
                column: "SpecId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RaidBuffSources");

            migrationBuilder.DropTable(
                name: "RaidBuffDefinitions");
        }
    }
}
