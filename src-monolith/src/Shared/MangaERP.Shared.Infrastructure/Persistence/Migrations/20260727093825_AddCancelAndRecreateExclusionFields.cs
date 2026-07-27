using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelAndRecreateExclusionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationCategory",
                table: "PageTasks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "PageTasks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousAssignedAssistantId",
                table: "PageTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecreatedAt",
                table: "PageTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecreatedByUserId",
                table: "PageTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecreatedFromTaskId",
                table: "PageTasks",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationCategory",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "PreviousAssignedAssistantId",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "RecreatedAt",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "RecreatedByUserId",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "RecreatedFromTaskId",
                table: "PageTasks");
        }
    }
}
