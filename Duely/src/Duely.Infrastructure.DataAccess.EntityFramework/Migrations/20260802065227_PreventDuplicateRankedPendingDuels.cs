using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateRankedPendingDuels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "PendingDuels" AS duplicate
                USING "PendingDuels" AS existing
                WHERE duplicate."Type" = 'Ranked'
                  AND existing."Type" = 'Ranked'
                  AND duplicate."UserId" = existing."UserId"
                  AND (duplicate."CreatedAt", duplicate."Id") > (existing."CreatedAt", existing."Id");
                """);

            migrationBuilder.DropIndex(
                name: "IX_PendingDuels_UserId",
                table: "PendingDuels");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_UserId",
                table: "PendingDuels",
                column: "UserId",
                unique: true,
                filter: "\"Type\" = 'Ranked'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingDuels_UserId",
                table: "PendingDuels");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_UserId",
                table: "PendingDuels",
                column: "UserId");
        }
    }
}
