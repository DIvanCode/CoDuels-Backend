using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class CreateSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SubmissionId",
                table: "IntegrationEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DuelId = table.Column<int>(type: "integer", nullable: false),
                    ProblemId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Submissions_DuelProblems_DuelId_ProblemId",
                        columns: x => new { x.DuelId, x.ProblemId },
                        principalTable: "DuelProblems",
                        principalColumns: new[] { "DuelId", "ProblemId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Submissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_SubmissionId",
                table: "IntegrationEvents",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_DuelId_ProblemId",
                table: "Submissions",
                columns: new[] { "DuelId", "ProblemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_DuelId_UserId_ProblemId",
                table: "Submissions",
                columns: new[] { "DuelId", "UserId", "ProblemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId_DuelId_ProblemId",
                table: "Submissions",
                columns: new[] { "UserId", "DuelId", "ProblemId" });

            migrationBuilder.AddForeignKey(
                name: "FK_IntegrationEvents_Submissions_SubmissionId",
                table: "IntegrationEvents",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntegrationEvents_Submissions_SubmissionId",
                table: "IntegrationEvents");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationEvents_SubmissionId",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "IntegrationEvents");
        }
    }
}
