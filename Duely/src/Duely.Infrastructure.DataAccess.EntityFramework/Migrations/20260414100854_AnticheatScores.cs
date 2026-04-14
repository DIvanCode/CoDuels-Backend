using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AnticheatScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnticheatScores",
                columns: table => new
                {
                    DuelId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TaskKey = table.Column<string>(type: "varchar(1)", nullable: false),
                    Score = table.Column<float>(type: "real", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnticheatScores", x => new { x.DuelId, x.UserId, x.TaskKey });
                    table.ForeignKey(
                        name: "FK_AnticheatScores_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnticheatScores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActions_DuelId_UserId_TaskKey_SequenceId",
                table: "UserActions",
                columns: new[] { "DuelId", "UserId", "TaskKey", "SequenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnticheatScores_UserId",
                table: "AnticheatScores",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnticheatScores");

            migrationBuilder.DropIndex(
                name: "IX_UserActions_DuelId_UserId_TaskKey_SequenceId",
                table: "UserActions");
        }
    }
}
