using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class NicknameLower : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Users"
                        GROUP BY lower("Nickname")
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Users contain nicknames that differ only by case. Resolve duplicates before applying the NicknameLower migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "NicknameLower",
                table: "Users",
                type: "text",
                nullable: false,
                computedColumnSql: "lower(\"Nickname\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NicknameLower",
                table: "Users",
                column: "NicknameLower",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NicknameLower",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NicknameLower",
                table: "Users");
        }
    }
}
