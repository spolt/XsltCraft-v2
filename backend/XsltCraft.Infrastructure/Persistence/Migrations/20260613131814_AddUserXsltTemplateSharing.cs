using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XsltCraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserXsltTemplateSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditingHeartbeatAt",
                table: "UserXsltTemplates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EditingUserId",
                table: "UserXsltTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserXsltTemplateShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserXsltTemplateShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserXsltTemplateShares_UserXsltTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "UserXsltTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserXsltTemplateShares_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserXsltTemplates_EditingUserId",
                table: "UserXsltTemplates",
                column: "EditingUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserXsltTemplateShares_TemplateId_UserId",
                table: "UserXsltTemplateShares",
                columns: new[] { "TemplateId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserXsltTemplateShares_UserId",
                table: "UserXsltTemplateShares",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserXsltTemplateShares");

            migrationBuilder.DropIndex(
                name: "IX_UserXsltTemplates_EditingUserId",
                table: "UserXsltTemplates");

            migrationBuilder.DropColumn(
                name: "EditingHeartbeatAt",
                table: "UserXsltTemplates");

            migrationBuilder.DropColumn(
                name: "EditingUserId",
                table: "UserXsltTemplates");
        }
    }
}
