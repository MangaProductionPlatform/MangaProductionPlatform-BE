using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2AssistantTaskWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "WorkStartedAt",
                table: "PageTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SeriesAccessGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollaborationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesAccessGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesAccessGrants_MangaSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "MangaSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesAccessGrants_MangakaAssistantCollaborations_Collabora~",
                        column: x => x.CollaborationId,
                        principalTable: "MangakaAssistantCollaborations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeriesAccessGrants_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SeriesAccessGrants_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskAssignmentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollaborationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignmentAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentAttempts_MangakaAssistantCollaborations_Colla~",
                        column: x => x.CollaborationId,
                        principalTable: "MangakaAssistantCollaborations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentAttempts_PageTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "PageTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentAttempts_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentAttempts_Users_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeriesAccessGrants_CollaborationId_SeriesId",
                table: "SeriesAccessGrants",
                columns: new[] { "CollaborationId", "SeriesId" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesAccessGrants_GrantedByUserId",
                table: "SeriesAccessGrants",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesAccessGrants_RevokedByUserId",
                table: "SeriesAccessGrants",
                column: "RevokedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesAccessGrants_SeriesId_CollaborationId",
                table: "SeriesAccessGrants",
                columns: new[] { "SeriesId", "CollaborationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_AssignedByUserId",
                table: "TaskAssignmentAttempts",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_AssistantId_Status",
                table: "TaskAssignmentAttempts",
                columns: new[] { "AssistantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_CollaborationId",
                table: "TaskAssignmentAttempts",
                column: "CollaborationId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_Accepted",
                table: "TaskAssignmentAttempts",
                column: "TaskId",
                unique: true,
                filter: "\"Status\" = 'Accepted'");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_AttemptNumber",
                table: "TaskAssignmentAttempts",
                columns: new[] { "TaskId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_PendingAcceptance",
                table: "TaskAssignmentAttempts",
                column: "TaskId",
                unique: true,
                filter: "\"Status\" = 'PendingAcceptance'");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentAttempts_TaskId_Status",
                table: "TaskAssignmentAttempts",
                columns: new[] { "TaskId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeriesAccessGrants");

            migrationBuilder.DropTable(
                name: "TaskAssignmentAttempts");

            migrationBuilder.DropColumn(
                name: "WorkStartedAt",
                table: "PageTasks");
        }
    }
}
