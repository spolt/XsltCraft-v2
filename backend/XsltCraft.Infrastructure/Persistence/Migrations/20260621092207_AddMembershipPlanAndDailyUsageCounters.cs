using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XsltCraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipPlanAndDailyUsageCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Free");

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiRequestCount",
                table: "UserAiUsages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TemplateExportCount",
                table: "UserAiUsages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "Templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AiRequestCount",
                table: "UserAiUsages");

            migrationBuilder.DropColumn(
                name: "TemplateExportCount",
                table: "UserAiUsages");

            migrationBuilder.DropColumn(
                name: "IsPremium",
                table: "Templates");
        }
    }
}
