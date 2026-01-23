using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RenameCodeToSolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ShouldShowOpponentCode",
                table: "DuelConfigurations",
                newName: "ShouldShowOpponentSolution");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ShouldShowOpponentSolution",
                table: "DuelConfigurations",
                newName: "ShouldShowOpponentCode");
        }
    }
}
