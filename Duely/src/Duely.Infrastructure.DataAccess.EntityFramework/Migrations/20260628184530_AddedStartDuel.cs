using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddedStartDuel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Duels",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Duels",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Duels");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Duels");
        }
    }
}
