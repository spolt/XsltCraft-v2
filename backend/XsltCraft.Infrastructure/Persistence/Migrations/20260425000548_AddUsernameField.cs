using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XsltCraft.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sütunu önce nullable olarak ekle — mevcut satırlar için backfill yapılacak
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Mevcut kullanıcıları email prefix + ID suffix ile doldur (benzersizlik için)
            migrationBuilder.Sql(
                @"UPDATE ""Users""
                  SET ""Username"" = LOWER(REGEXP_REPLACE(SPLIT_PART(""Email"", '@', 1), '[^a-zA-Z0-9_]', '', 'g'))
                                   || '_' || SUBSTRING(REPLACE(CAST(""Id"" AS text), '-', ''), 1, 6)
                  WHERE ""Username"" IS NULL OR ""Username"" = ''");

            // NOT NULL'a geç
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");
        }
    }
}
