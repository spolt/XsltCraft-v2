using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XsltCraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixAssetTemplateRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Templates_TemplateId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_TemplateId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Assets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_TemplateId",
                table: "Assets",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Templates_TemplateId",
                table: "Assets",
                column: "TemplateId",
                principalTable: "Templates",
                principalColumn: "Id");
        }
    }
}
