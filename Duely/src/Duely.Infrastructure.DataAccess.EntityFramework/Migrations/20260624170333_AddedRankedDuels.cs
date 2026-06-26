using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddedRankedDuels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DuelConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsRated = table.Column<bool>(type: "boolean", nullable: false),
                    ShouldShowOpponentSolution = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    ProblemsCount = table.Column<int>(type: "integer", nullable: false),
                    ProblemsOrder = table.Column<string>(type: "text", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuelConfigurations_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Duels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ConfigurationId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Duels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Duels_DuelConfigurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DuelParticipants",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DuelId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    InitialRating = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuelParticipants", x => new { x.UserId, x.DuelId });
                    table.ForeignKey(
                        name: "FK_DuelParticipants_Duels_DuelId",
                        column: x => x.DuelId,
                        principalTable: "Duels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DuelParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_DuelConfigurations_CreatedById",
                table: "DuelConfigurations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DuelParticipants_DuelId",
                table: "DuelParticipants",
                column: "DuelId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_ConfigurationId",
                table: "Duels",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_RankedDuelRankedDuelParticipant_ParticipantsUserId_Particip~",
                table: "RankedDuelRankedDuelParticipant",
                columns: new[] { "ParticipantsUserId", "ParticipantsDuelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankedDuelRankedDuelParticipant");

            migrationBuilder.DropTable(
                name: "DuelParticipants");

            migrationBuilder.DropTable(
                name: "Duels");

            migrationBuilder.DropTable(
                name: "DuelConfigurations");
        }
    }
}
