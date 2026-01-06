using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class LinkDuelConfigurationToDuel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TaskId",
                table: "Duels",
                newName: "Tasks");

            migrationBuilder.RenameColumn(
                name: "ShowOpponentCode",
                table: "DuelConfigurations",
                newName: "ShouldShowOpponentCode");

            migrationBuilder.AddColumn<string>(
                name: "TaskKey",
                table: "Submissions",
                type: "varchar(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ConfigurationId",
                table: "Duels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsRated",
                table: "DuelConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Duels_ConfigurationId",
                table: "Duels",
                column: "ConfigurationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Duels_DuelConfigurations_ConfigurationId",
                table: "Duels",
                column: "ConfigurationId",
                principalTable: "DuelConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Duels_DuelConfigurations_ConfigurationId",
                table: "Duels");

            migrationBuilder.DropIndex(
                name: "IX_Duels_ConfigurationId",
                table: "Duels");

            migrationBuilder.DropColumn(
                name: "TaskKey",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ConfigurationId",
                table: "Duels");

            migrationBuilder.DropColumn(
                name: "IsRated",
                table: "DuelConfigurations");

            migrationBuilder.RenameColumn(
                name: "Tasks",
                table: "Duels",
                newName: "TaskId");

            migrationBuilder.RenameColumn(
                name: "ShouldShowOpponentCode",
                table: "DuelConfigurations",
                newName: "ShowOpponentCode");
        }
    }
}
