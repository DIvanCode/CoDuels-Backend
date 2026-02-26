using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class PendingDuels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingDuels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    FriendlyPendingDuel_User1Id = table.Column<int>(type: "integer", nullable: true),
                    FriendlyPendingDuel_User2Id = table.Column<int>(type: "integer", nullable: true),
                    FriendlyPendingDuel_ConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    User1Id = table.Column<int>(type: "integer", nullable: true),
                    User2Id = table.Column<int>(type: "integer", nullable: true),
                    ConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    IsAcceptedByUser1 = table.Column<bool>(type: "boolean", nullable: true),
                    IsAcceptedByUser2 = table.Column<bool>(type: "boolean", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingDuels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingDuels_DuelConfigurations_ConfigurationId",
                        column: x => x.ConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PendingDuels_DuelConfigurations_FriendlyPendingDuel_Configu~",
                        column: x => x.FriendlyPendingDuel_ConfigurationId,
                        principalTable: "DuelConfigurations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PendingDuels_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_FriendlyPendingDuel_User1Id",
                        column: x => x.FriendlyPendingDuel_User1Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_FriendlyPendingDuel_User2Id",
                        column: x => x.FriendlyPendingDuel_User2Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_User1Id",
                        column: x => x.User1Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_User2Id",
                        column: x => x.User2Id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingDuels_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_ConfigurationId",
                table: "PendingDuels",
                column: "ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_CreatedById",
                table: "PendingDuels",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_FriendlyPendingDuel_ConfigurationId",
                table: "PendingDuels",
                column: "FriendlyPendingDuel_ConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_FriendlyPendingDuel_User1Id",
                table: "PendingDuels",
                column: "FriendlyPendingDuel_User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_FriendlyPendingDuel_User2Id",
                table: "PendingDuels",
                column: "FriendlyPendingDuel_User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_GroupId",
                table: "PendingDuels",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_User1Id",
                table: "PendingDuels",
                column: "User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_User2Id",
                table: "PendingDuels",
                column: "User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingDuels_UserId",
                table: "PendingDuels",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingDuels");
        }
    }
}
