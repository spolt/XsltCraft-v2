using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XsltCraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFoldersAndFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "UserXsltTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "UserXsltTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "Templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "Templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Color = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Folders_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserXsltTemplates_FolderId",
                table: "UserXsltTemplates",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_FolderId",
                table: "Templates",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_OwnerId_Kind",
                table: "Folders",
                columns: new[] { "OwnerId", "Kind" });

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Folders_FolderId",
                table: "Templates",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserXsltTemplates_Folders_FolderId",
                table: "UserXsltTemplates",
                column: "FolderId",
                principalTable: "Folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Folders_FolderId",
                table: "Templates");

            migrationBuilder.DropForeignKey(
                name: "FK_UserXsltTemplates_Folders_FolderId",
                table: "UserXsltTemplates");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_UserXsltTemplates_FolderId",
                table: "UserXsltTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Templates_FolderId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "UserXsltTemplates");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "UserXsltTemplates");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "Templates");
        }
    }
}
