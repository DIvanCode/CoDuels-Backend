using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddedStartDuelIntegrationEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DuelId",
                table: "IntegrationEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_DuelId",
                table: "IntegrationEvents",
                column: "DuelId");

            migrationBuilder.AddForeignKey(
                name: "FK_IntegrationEvents_Duels_DuelId",
                table: "IntegrationEvents",
                column: "DuelId",
                principalTable: "Duels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IntegrationEvents_Duels_DuelId",
                table: "IntegrationEvents");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationEvents_DuelId",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "DuelId",
                table: "IntegrationEvents");
        }
    }
}
