using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class DuelUserRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.RenameColumn(
                name: "User2RatingDelta",
                table: "Duels",
                newName: "User2FinalRating");

            migrationBuilder.RenameColumn(
                name: "User1RatingDelta",
                table: "Duels",
                newName: "User1FinalRating");

            migrationBuilder.AlterColumn<int>(
                name: "User2Id",
                table: "Duels",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "User1Id",
                table: "Duels",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "User1InitRating",
                table: "Duels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "User2InitRating",
                table: "Duels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Duels_Users_User1Id",
                table: "Duels",
                column: "User1Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Duels_Users_User2Id",
                table: "Duels",
                column: "User2Id",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Duels_Users_WinnerId",
                table: "Duels",
                column: "WinnerId",
                principalTable: "Users",
                principalColumn: "Id");
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

            migrationBuilder.DropColumn(
                name: "User1InitRating",
                table: "Duels");

            migrationBuilder.DropColumn(
                name: "User2InitRating",
                table: "Duels");

            migrationBuilder.RenameColumn(
                name: "User2FinalRating",
                table: "Duels",
                newName: "User2RatingDelta");

            migrationBuilder.RenameColumn(
                name: "User1FinalRating",
                table: "Duels",
                newName: "User1RatingDelta");

            migrationBuilder.AlterColumn<int>(
                name: "User2Id",
                table: "Duels",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "User1Id",
                table: "Duels",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

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
        }
    }
}
