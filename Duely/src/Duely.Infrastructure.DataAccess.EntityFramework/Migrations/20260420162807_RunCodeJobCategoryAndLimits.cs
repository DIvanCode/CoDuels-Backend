using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class RunCodeJobCategoryAndLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: schema changes were already applied in 20260420121500_AddDuelIdAndTaskKeyToCodeRuns.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op.
        }
    }
}
