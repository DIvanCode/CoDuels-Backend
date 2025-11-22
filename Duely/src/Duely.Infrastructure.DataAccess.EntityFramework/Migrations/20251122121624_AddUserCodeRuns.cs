using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCodeRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCodeRuns_Duels_DuelId",
                table: "UserCodeRuns");

            migrationBuilder.DropIndex(
                name: "IX_UserCodeRuns_DuelId",
                table: "UserCodeRuns");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserCodeRuns");

            migrationBuilder.DropColumn(
                name: "DuelId",
                table: "UserCodeRuns");

            migrationBuilder.DropColumn(
                name: "Verdict",
                table: "UserCodeRuns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UserCodeRuns",
                type: "timestamp",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DuelId",
                table: "UserCodeRuns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Verdict",
                table: "UserCodeRuns",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCodeRuns_DuelId",
                table: "UserCodeRuns",
                column: "DuelId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCodeRuns_Duels_DuelId",
                table: "UserCodeRuns",
                column: "DuelId",
                principalTable: "Duels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
