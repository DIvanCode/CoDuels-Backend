using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RenameCodeRunsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCodeRuns");

            migrationBuilder.RenameColumn(
                name: "DuelTicket",
                table: "Users",
                newName: "AuthTicket");

            migrationBuilder.RenameColumn(
                name: "IsUpsolve",
                table: "Submissions",
                newName: "IsUpsolving");

            migrationBuilder.RenameColumn(
                name: "Code",
                table: "Submissions",
                newName: "Solution");

            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                table: "Users",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1500);

            migrationBuilder.CreateTable(
                name: "CodeRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Input = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ExecutionId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_UserId",
                table: "CodeRuns",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeRuns");

            migrationBuilder.RenameColumn(
                name: "AuthTicket",
                table: "Users",
                newName: "DuelTicket");

            migrationBuilder.RenameColumn(
                name: "Solution",
                table: "Submissions",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "IsUpsolving",
                table: "Submissions",
                newName: "IsUpsolve");

            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 1500,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "UserCodeRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ExecutionId = table.Column<string>(type: "text", nullable: true),
                    Input = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCodeRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCodeRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCodeRuns_UserId",
                table: "UserCodeRuns",
                column: "UserId");
        }
    }
}
