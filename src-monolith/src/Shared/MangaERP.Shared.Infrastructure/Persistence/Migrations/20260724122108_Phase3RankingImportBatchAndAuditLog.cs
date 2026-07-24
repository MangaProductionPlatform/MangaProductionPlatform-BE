using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3RankingImportBatchAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_StudioInvitations_SeriesId_NormalizedAssistantEmail_Status\";");

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "SystemAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewState",
                table: "SystemAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousState",
                table: "SystemAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "SystemAuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RankingImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Filename = table.Column<string>(type: "text", nullable: false),
                    FileChecksum = table.Column<string>(type: "text", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    PeriodIdentifier = table.Column<string>(type: "text", nullable: true),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingImportBatches", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignmentAttempts_MangakaAssistantCollaborations_Colla~",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignmentAttempts_PageTasks_TaskId",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignmentAttempts_Users_AssignedByUserId",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskAssignmentAttempts_Users_AssistantId",
                table: "TaskAssignmentAttempts");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "CollaborationEvents");

            migrationBuilder.DropTable(
                name: "RankingImportBatches");

            migrationBuilder.DropTable(
                name: "SeriesAccessGrants");

            migrationBuilder.DropTable(
                name: "TaskCheckpoints");

            migrationBuilder.DropTable(
                name: "TaskProgressUpdates");

            migrationBuilder.DropTable(
                name: "MangakaAssistantCollaborations");

            migrationBuilder.DropIndex(
                name: "IX_StudioInvitations_InviterMangakaId_NormalizedAssistantEmail~",
                table: "StudioInvitations");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "SystemAuditLogs");

            migrationBuilder.DropColumn(
                name: "NewState",
                table: "SystemAuditLogs");

            migrationBuilder.DropColumn(
                name: "PreviousState",
                table: "SystemAuditLogs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "SystemAuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_StudioInvitations_SeriesId_NormalizedAssistantEmail_Status",
                table: "StudioInvitations",
                columns: new[] { "SeriesId", "NormalizedAssistantEmail", "Status" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }
    }
}
