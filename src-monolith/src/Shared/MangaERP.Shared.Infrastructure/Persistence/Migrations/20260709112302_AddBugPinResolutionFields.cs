using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBugPinResolutionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolvedNote",
                table: "BugPins",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedLayerId",
                table: "BugPins",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolvedNote",
                table: "BugPins");

            migrationBuilder.DropColumn(
                name: "ReviewedLayerId",
                table: "BugPins");
        }
    }
}
