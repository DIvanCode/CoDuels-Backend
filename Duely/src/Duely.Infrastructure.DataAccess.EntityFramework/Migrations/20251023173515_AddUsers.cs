using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Duel_DuelId",
                table: "Submissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Duel",
                table: "Duel");

            migrationBuilder.DropColumn(
                name: "MaxDuration",
                table: "Duel");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "Duel");

            migrationBuilder.RenameTable(
                name: "Duel",
                newName: "Duels");

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Submissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadlineTime",
                table: "Duels",
                type: "timestamp",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "WinnerId",
                table: "Duels",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Duels",
                table: "Duels",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nickname = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PasswordSalt = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId",
                table: "Submissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_User1Id",
                table: "Duels",
                column: "User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_User2Id",
                table: "Duels",
                column: "User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_Duels_WinnerId",
                table: "Duels",
                column: "WinnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Duels_Users_User1Id",
                table: "Duels",
                column: "User1Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Duels_Users_User2Id",
                table: "Duels",
                column: "User2Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Duels_Users_WinnerId",
                table: "Duels",
                column: "WinnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Duels_DuelId",
                table: "Submissions",
                column: "DuelId",
                principalTable: "Duels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Users_UserId",
                table: "Submissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Duels_Users_User1Id",
                table: "Duels");

            migrationBuilder.DropForeignKey(
                name: "FK_Duels_Users_User2Id",
                table: "Duels");

            migrationBuilder.DropForeignKey(
                name: "FK_Duels_Users_WinnerId",
                table: "Duels");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Duels_DuelId",
                table: "Submissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_Users_UserId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_UserId",
                table: "Submissions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Duels",
                table: "Duels");

            migrationBuilder.DropIndex(
                name: "IX_Duels_User1Id",
                table: "Duels");

            migrationBuilder.DropIndex(
                name: "IX_Duels_User2Id",
                table: "Duels");

            migrationBuilder.DropIndex(
                name: "IX_Duels_WinnerId",
                table: "Duels");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "DeadlineTime",
                table: "Duels");

            migrationBuilder.DropColumn(
                name: "WinnerId",
                table: "Duels");

            migrationBuilder.RenameTable(
                name: "Duels",
                newName: "Duel");

            migrationBuilder.AddColumn<int>(
                name: "MaxDuration",
                table: "Duel",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "Result",
                table: "Duel",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Duel",
                table: "Duel",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_Duel_DuelId",
                table: "Submissions",
                column: "DuelId",
                principalTable: "Duel",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
