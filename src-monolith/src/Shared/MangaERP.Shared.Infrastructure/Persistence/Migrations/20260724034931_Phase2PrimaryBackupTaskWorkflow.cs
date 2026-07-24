using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2PrimaryBackupTaskWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignmentRole",
                table: "TaskAssignmentAttempts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Primary");

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousAttemptId",
                table: "TaskAssignmentAttempts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseDeadline",
                table: "TaskAssignmentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WorkDeadline",
                table: "TaskAssignmentAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BackupAssistantId",
                table: "PageTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentAssignmentAttemptId",
                table: "PageTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryAssistantId",
                table: "PageTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReassignmentReason",
                table: "PageTasks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReassignmentRequiredAt",
                table: "PageTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TakeoverStatus",
                table: "PageTasks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentRole",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropColumn(
                name: "PreviousAttemptId",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropColumn(
                name: "ResponseDeadline",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropColumn(
                name: "WorkDeadline",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropColumn(
                name: "BackupAssistantId",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "CurrentAssignmentAttemptId",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "PrimaryAssistantId",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "ReassignmentReason",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "ReassignmentRequiredAt",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "TakeoverStatus",
                table: "PageTasks");
        }
    }
}
