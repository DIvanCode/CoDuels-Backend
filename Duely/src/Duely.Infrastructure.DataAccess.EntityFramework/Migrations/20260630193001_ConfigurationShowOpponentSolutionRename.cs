using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurationShowOpponentSolutionRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ShouldShowOpponentSolution",
                table: "DuelConfigurations",
                newName: "ShowOpponentSolution");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ShowOpponentSolution",
                table: "DuelConfigurations",
                newName: "ShouldShowOpponentSolution");
        }
    }
}
