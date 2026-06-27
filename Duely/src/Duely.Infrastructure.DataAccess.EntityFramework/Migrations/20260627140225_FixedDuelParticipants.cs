using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class FixedDuelParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankedDuelRankedDuelParticipant");

            migrationBuilder.AddColumn<bool>(
                name: "IsReady",
                table: "DuelParticipants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReady",
                table: "DuelParticipants");

            migrationBuilder.CreateTable(
                name: "RankedDuelRankedDuelParticipant",
                columns: table => new
                {
                    RankedDuelId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantsUserId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantsDuelId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankedDuelRankedDuelParticipant", x => new { x.RankedDuelId, x.ParticipantsUserId, x.ParticipantsDuelId });
                    table.ForeignKey(
                        name: "FK_RankedDuelRankedDuelParticipant_DuelParticipants_Participan~",
                        columns: x => new { x.ParticipantsUserId, x.ParticipantsDuelId },
                        principalTable: "DuelParticipants",
                        principalColumns: new[] { "UserId", "DuelId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RankedDuelRankedDuelParticipant_Duels_RankedDuelId",
                        column: x => x.RankedDuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RankedDuelRankedDuelParticipant_ParticipantsUserId_Particip~",
                table: "RankedDuelRankedDuelParticipant",
                columns: new[] { "ParticipantsUserId", "ParticipantsDuelId" });
        }
    }
}
