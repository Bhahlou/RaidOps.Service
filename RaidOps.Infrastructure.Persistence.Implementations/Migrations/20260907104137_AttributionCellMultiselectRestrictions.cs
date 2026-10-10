using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaidOps.Infrastructure.Persistence.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AttributionCellMultiselectRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttributionDefinitionCells_Specs_RequiredSpecId",
                table: "AttributionDefinitionCells");

            migrationBuilder.DropForeignKey(
                name: "FK_AttributionDefinitionCells_WowClasses_RequiredClassId",
                table: "AttributionDefinitionCells");

            migrationBuilder.DropIndex(
                name: "IX_AttributionDefinitionCells_RequiredClassId",
                table: "AttributionDefinitionCells");

            migrationBuilder.DropIndex(
                name: "IX_AttributionDefinitionCells_RequiredSpecId",
                table: "AttributionDefinitionCells");

            migrationBuilder.DropColumn(
                name: "RequiredClassId",
                table: "AttributionDefinitionCells");

            migrationBuilder.DropColumn(
                name: "RequiredRole",
                table: "AttributionDefinitionCells");

            migrationBuilder.DropColumn(
                name: "RequiredSpecId",
                table: "AttributionDefinitionCells");

            migrationBuilder.AddColumn<List<int>>(
                name: "RequiredClassIds",
                table: "AttributionDefinitionCells",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<int[]>(
                name: "RequiredRoles",
                table: "AttributionDefinitionCells",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<List<int>>(
                name: "RequiredSpecIds",
                table: "AttributionDefinitionCells",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredClassIds",
                table: "AttributionDefinitionCells");

            migrationBuilder.DropColumn(
                name: "RequiredRoles",
                table: "AttributionDefinitionCells");

            migrationBuilder.DropColumn(
                name: "RequiredSpecIds",
                table: "AttributionDefinitionCells");

            migrationBuilder.AddColumn<int>(
                name: "RequiredClassId",
                table: "AttributionDefinitionCells",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredRole",
                table: "AttributionDefinitionCells",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredSpecId",
                table: "AttributionDefinitionCells",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributionDefinitionCells_RequiredClassId",
                table: "AttributionDefinitionCells",
                column: "RequiredClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributionDefinitionCells_RequiredSpecId",
                table: "AttributionDefinitionCells",
                column: "RequiredSpecId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttributionDefinitionCells_Specs_RequiredSpecId",
                table: "AttributionDefinitionCells",
                column: "RequiredSpecId",
                principalTable: "Specs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AttributionDefinitionCells_WowClasses_RequiredClassId",
                table: "AttributionDefinitionCells",
                column: "RequiredClassId",
                principalTable: "WowClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
