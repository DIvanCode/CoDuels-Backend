using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class ObservabilitySubmissionCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Submissions",
                type: "timestamp",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_Status_SubmitTime",
                table: "Submissions",
                columns: new[] { "Status", "SubmitTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_Verdict_CompletedAt",
                table: "Submissions",
                columns: new[] { "Verdict", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeRuns_Status_CreatedAt",
                table: "CodeRuns",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_Status_SubmitTime",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_Verdict_CompletedAt",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_CodeRuns_Status_CreatedAt",
                table: "CodeRuns");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Submissions");
        }
    }
}
