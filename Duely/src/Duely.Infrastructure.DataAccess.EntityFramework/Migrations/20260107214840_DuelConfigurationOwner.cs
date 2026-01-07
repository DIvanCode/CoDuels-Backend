using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class DuelConfigurationOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "DuelConfigurations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuelConfigurations_OwnerId",
                table: "DuelConfigurations",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_DuelConfigurations_Users_OwnerId",
                table: "DuelConfigurations",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DuelConfigurations_Users_OwnerId",
                table: "DuelConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_DuelConfigurations_OwnerId",
                table: "DuelConfigurations");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "DuelConfigurations");
        }
    }
}
