using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2TaskAssignmentEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PageTasks_ChapterId_PageNumber",
                table: "PageTasks");

            migrationBuilder.AddColumn<DateTime>(
                name: "HalfwayWarningSentAt",
                table: "PageTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PageTasks_ChapterId_PageNumber",
                table: "PageTasks",
                columns: new[] { "ChapterId", "PageNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PageTasks_ChapterId_PageNumber",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "HalfwayWarningSentAt",
                table: "PageTasks");

            migrationBuilder.CreateIndex(
                name: "IX_PageTasks_ChapterId_PageNumber",
                table: "PageTasks",
                columns: new[] { "ChapterId", "PageNumber" },
                unique: true);
        }
    }
}
