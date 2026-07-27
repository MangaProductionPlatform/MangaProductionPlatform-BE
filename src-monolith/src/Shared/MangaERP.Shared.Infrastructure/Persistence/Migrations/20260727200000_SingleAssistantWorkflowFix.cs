using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleAssistantWorkflowFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_AssignmentRole_PendingAcceptance",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_AssignmentRole_Accepted",
                table: "TaskAssignmentAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_PendingAcceptance",
                table: "TaskAssignmentAttempts",
                column: "TaskId",
                unique: true,
                filter: "\"Status\" = 'PendingAcceptance'");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_Accepted",
                table: "TaskAssignmentAttempts",
                column: "TaskId",
                unique: true,
                filter: "\"Status\" = 'Accepted'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_PendingAcceptance",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_Accepted",
                table: "TaskAssignmentAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_AssignmentRole_PendingAcceptance",
                table: "TaskAssignmentAttempts",
                columns: new[] { "TaskId", "AssignmentRole" },
                unique: true,
                filter: "\"Status\" = 'PendingAcceptance'");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_AssignmentRole_Accepted",
                table: "TaskAssignmentAttempts",
                columns: new[] { "TaskId", "AssignmentRole" },
                unique: true,
                filter: "\"Status\" = 'Accepted'");
        }
    }
}
