using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class DuelRatingDelta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "User1RatingDelta",
                table: "Duels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "User2RatingDelta",
                table: "Duels",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "User1RatingDelta",
                table: "Duels");

            migrationBuilder.DropColumn(
                name: "User2RatingDelta",
                table: "Duels");
        }
    }
}
