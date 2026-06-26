using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duely.Infrastructure.DataAccess.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddedSendMessageIntegrationEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "ExpirationTime",
                table: "IntegrationEvents",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "IntegrationEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpirationTime",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "IntegrationEvents");
        }
    }
}
