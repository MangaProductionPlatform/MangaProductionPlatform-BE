using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAssistantWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                table: "PageTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollaborationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetPercent = table.Column<int>(type: "integer", nullable: false),
                    OffsetMinutesFromAcceptance = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskCheckpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskCheckpoints_PageTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "PageTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskProgressUpdates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentAttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskProgressUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskProgressUpdates_PageTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "PageTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorUserId",
                table: "AuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CollaborationId",
                table: "AuditEvents",
                column: "CollaborationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_SeriesId",
                table: "AuditEvents",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TaskId",
                table: "AuditEvents",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskCheckpoints_TaskId",
                table: "TaskCheckpoints",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgressUpdates_AssistantId",
                table: "TaskProgressUpdates",
                column: "AssistantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskProgressUpdates_TaskId",
                table: "TaskProgressUpdates",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "TaskCheckpoints");

            migrationBuilder.DropTable(
                name: "TaskProgressUpdates");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "PageTasks");
        }
    }
}
