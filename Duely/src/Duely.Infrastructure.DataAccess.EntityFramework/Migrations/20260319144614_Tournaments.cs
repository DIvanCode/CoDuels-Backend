using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class Tournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TournamentId",
                table: "PendingDuels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TournamentPendingDuel_ConfigurationId",
                table: "PendingDuels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TournamentPendingDuel_User1Id",
                table: "PendingDuels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TournamentPendingDuel_User2Id",
                table: "PendingDuels",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    MatchmakingType = table.Column<string>(type: "text", nullable: false),
                    DuelConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    Nodes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tournaments_DuelConfigurations_DuelConfigurationId",
                        column: x => x.DuelConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tournaments_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tournaments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentParticipants",
                columns: table => new
                {
                    TournamentId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentParticipants", x => new { x.TournamentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TournamentParticipants_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TournamentParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_TournamentId",
                table: "PendingDuels",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_ConfigurationId",
                table: "PendingDuels",
                column: "TournamentPendingDuel_ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_User1Id",
                table: "PendingDuels",
                column: "TournamentPendingDuel_User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_User2Id",
                table: "PendingDuels",
                column: "TournamentPendingDuel_User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentParticipants_UserId",
                table: "TournamentParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_CreatedById",
                table: "Tournaments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_DuelConfigurationId",
                table: "Tournaments",
                column: "DuelConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_Tournaments_GroupId",
                table: "Tournaments",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_PendingDuels_DuelConfigurations_TournamentPendingDuel_Confi~",
                table: "PendingDuels",
                column: "TournamentPendingDuel_ConfigurationId",
                principalTable: "DuelConfigurations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PendingDuels_Tournaments_TournamentId",
                table: "PendingDuels",
                column: "TournamentId",
                principalTable: "Tournaments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PendingDuels_Users_TournamentPendingDuel_User1Id",
                table: "PendingDuels",
                column: "TournamentPendingDuel_User1Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PendingDuels_Users_TournamentPendingDuel_User2Id",
                table: "PendingDuels",
                column: "TournamentPendingDuel_User2Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PendingDuels_DuelConfigurations_TournamentPendingDuel_Confi~",
                table: "PendingDuels");

            migrationBuilder.DropForeignKey(
                name: "FK_PendingDuels_Tournaments_TournamentId",
                table: "PendingDuels");

            migrationBuilder.DropForeignKey(
                name: "FK_PendingDuels_Users_TournamentPendingDuel_User1Id",
                table: "PendingDuels");

            migrationBuilder.DropForeignKey(
                name: "FK_PendingDuels_Users_TournamentPendingDuel_User2Id",
                table: "PendingDuels");

            migrationBuilder.DropTable(
                name: "TournamentParticipants");

            migrationBuilder.DropTable(
                name: "Tournaments");

            migrationBuilder.DropIndex(
                name: "IX_PendingDuels_TournamentId",
                table: "PendingDuels");

            migrationBuilder.DropIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_ConfigurationId",
                table: "PendingDuels");

            migrationBuilder.DropIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_User1Id",
                table: "PendingDuels");

            migrationBuilder.DropIndex(
                name: "IX_PendingDuels_TournamentPendingDuel_User2Id",
                table: "PendingDuels");

            migrationBuilder.DropColumn(
                name: "TournamentId",
                table: "PendingDuels");

            migrationBuilder.DropColumn(
                name: "TournamentPendingDuel_ConfigurationId",
                table: "PendingDuels");

            migrationBuilder.DropColumn(
                name: "TournamentPendingDuel_User1Id",
                table: "PendingDuels");

            migrationBuilder.DropColumn(
                name: "TournamentPendingDuel_User2Id",
                table: "PendingDuels");
        }
    }
}
