using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RunCodeJobCategoryAndLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DuelId",
                table: "CodeRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TaskKey",
                table: "CodeRuns",
                type: "varchar(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_DuelId_TaskKey",
                table: "CodeRuns",
                columns: new[] { "DuelId", "TaskKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CodeRuns_DuelId_TaskKey",
                table: "CodeRuns");

            migrationBuilder.DropColumn(
                name: "DuelId",
                table: "CodeRuns");

            migrationBuilder.DropColumn(
                name: "TaskKey",
                table: "CodeRuns");
        }
    }
}
