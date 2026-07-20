using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeEditorialWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudioInvitations_SeriesId_AssistantEmail",
                table: "StudioInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPersonalEmail",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedAssistantEmail",
                table: "StudioInvitations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationDeliveryAttemptedAt",
                table: "StudioInvitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationDeliveryError",
                table: "StudioInvitations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationDeliveryStatus",
                table: "StudioInvitations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotRequired");

            migrationBuilder.AddColumn<DateTime>(
                name: "RecommendedAt",
                table: "SeriesSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TantouGuidance",
                table: "SeriesSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TantouReviewedAt",
                table: "SeriesSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditorialFeedback",
                table: "Chapters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EditorialRound",
                table: "Chapters",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TantouGuidance",
                table: "Chapters",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Users"
                        WHERE "PersonalEmail" IS NOT NULL AND trim("PersonalEmail") <> ''
                        GROUP BY lower(trim("PersonalEmail")) HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Duplicate PersonalEmail values must be merged by an administrator before FinalizeEditorialWorkflow can run.';
                    END IF;
                END $$;
                """);
            migrationBuilder.Sql("UPDATE \"Users\" SET \"NormalizedPersonalEmail\" = lower(trim(\"PersonalEmail\")) WHERE \"PersonalEmail\" IS NOT NULL AND trim(\"PersonalEmail\") <> ''; ");
            migrationBuilder.Sql("UPDATE \"StudioInvitations\" SET \"NormalizedAssistantEmail\" = lower(trim(\"AssistantEmail\"));");
            migrationBuilder.Sql("UPDATE \"StudioInvitations\" SET \"RegistrationDeliveryStatus\" = 'Sent' WHERE \"IsNewAccountFlow\" = TRUE AND \"ActivationToken\" IS NOT NULL;");
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "StudioInvitations"
                        WHERE "Status" = 'Pending'
                        GROUP BY "SeriesId", lower(trim("AssistantEmail"))
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Duplicate pending StudioInvitations require explicit administrator cleanup before FinalizeEditorialWorkflow can run.';
                    END IF;
                END $$;
                """);
            migrationBuilder.Sql("UPDATE \"Chapters\" SET \"EditorialRound\" = 1 WHERE \"EditorialRound\" = 0;");
            migrationBuilder.Sql("""
                UPDATE "SeriesSubmissions" AS submission
                SET "AssignedEditorId" = author."ManagingTantouId"
                FROM "Users" AS author
                WHERE submission."SubmitterId" = author."Id"
                  AND submission."AssignedEditorId" IS NULL
                  AND submission."Status" IN ('Pending_EB_Review', 'Requires_Revision', 'EB_Rejected', 'Conflict_Escalated');

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "SeriesSubmissions"
                        WHERE "Status" IN ('Pending_EB_Review', 'Requires_Revision', 'EB_Rejected', 'Conflict_Escalated')
                          AND "AssignedEditorId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Legacy active submissions without an assigned Tantou must be assigned before FinalizeEditorialWorkflow can run.';
                    END IF;
                END $$;

                UPDATE "SeriesSubmissions"
                SET "Status" = 'Pending_Tantou_Review', "CurrentRound" = "CurrentRound" + 1
                WHERE "Status" IN ('Pending_EB_Review', 'Conflict_Escalated');
                UPDATE "SeriesSubmissions" SET "Status" = 'Mangaka_Revision_Required'
                WHERE "Status" = 'Requires_Revision';
                UPDATE "SeriesSubmissions" SET "Status" = 'Editorial_Rejected_To_Tantou'
                WHERE "Status" = 'EB_Rejected';
                """);

            migrationBuilder.CreateTable(
                name: "DeadlineExtensionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequestedDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HandledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeadlineExtensionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EditorialReviewAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WorkId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditorialReviewAssignments", x => x.Id);
                    table.CheckConstraint("CK_EditorialReviewAssignments_Completion", "(\"Status\" = 'Pending' AND \"Decision\" IS NULL AND \"ReviewedAt\" IS NULL) OR (\"Status\" = 'Completed' AND \"Decision\" IN ('Approved', 'Rejected') AND \"ReviewedAt\" IS NOT NULL AND (\"Decision\" = 'Approved' OR length(trim(\"Feedback\")) > 0))");
                    table.CheckConstraint("CK_EditorialReviewAssignments_Round", "\"RoundNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_EditorialReviewAssignments_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedPersonalEmail",
                table: "Users",
                column: "NormalizedPersonalEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudioInvitations_SeriesId_NormalizedAssistantEmail_Status",
                table: "StudioInvitations",
                columns: new[] { "SeriesId", "NormalizedAssistantEmail", "Status" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions",
                sql: "\"Status\" IN ('Draft', 'Pending_Tantou_Review', 'Tantou_Revision_Required', 'Pending_EB_Review', 'Editorial_Rejected_To_Tantou', 'Mangaka_Revision_Required', 'EB_Approved', 'Conflict_Escalated')");

            migrationBuilder.CreateIndex(
                name: "IX_EditorialReviewAssignments_ReviewerId_Status",
                table: "EditorialReviewAssignments",
                columns: new[] { "ReviewerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialReviewAssignments_WorkType_WorkId_RoundNumber",
                table: "EditorialReviewAssignments",
                columns: new[] { "WorkType", "WorkId", "RoundNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_EditorialReviewAssignments_WorkType_WorkId_RoundNumber_Revi~",
                table: "EditorialReviewAssignments",
                columns: new[] { "WorkType", "WorkId", "RoundNumber", "ReviewerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeadlineExtensionRequests");

            migrationBuilder.DropTable(
                name: "EditorialReviewAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedPersonalEmail",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_StudioInvitations_SeriesId_NormalizedAssistantEmail_Status",
                table: "StudioInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions");

            migrationBuilder.Sql("""
                UPDATE "SeriesSubmissions" SET "Status" = 'Pending_EB_Review'
                WHERE "Status" = 'Pending_Tantou_Review';
                UPDATE "SeriesSubmissions" SET "Status" = 'Requires_Revision'
                WHERE "Status" IN ('Tantou_Revision_Required', 'Mangaka_Revision_Required');
                UPDATE "SeriesSubmissions" SET "Status" = 'EB_Rejected'
                WHERE "Status" = 'Editorial_Rejected_To_Tantou';
                """);

            migrationBuilder.DropColumn(
                name: "NormalizedPersonalEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedAssistantEmail",
                table: "StudioInvitations");

            migrationBuilder.DropColumn(
                name: "RegistrationDeliveryAttemptedAt",
                table: "StudioInvitations");

            migrationBuilder.DropColumn(
                name: "RegistrationDeliveryError",
                table: "StudioInvitations");

            migrationBuilder.DropColumn(
                name: "RegistrationDeliveryStatus",
                table: "StudioInvitations");

            migrationBuilder.DropColumn(
                name: "RecommendedAt",
                table: "SeriesSubmissions");

            migrationBuilder.DropColumn(
                name: "TantouGuidance",
                table: "SeriesSubmissions");

            migrationBuilder.DropColumn(
                name: "TantouReviewedAt",
                table: "SeriesSubmissions");

            migrationBuilder.DropColumn(
                name: "EditorialFeedback",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "EditorialRound",
                table: "Chapters");

            migrationBuilder.DropColumn(
                name: "TantouGuidance",
                table: "Chapters");

            migrationBuilder.CreateIndex(
                name: "IX_StudioInvitations_SeriesId_AssistantEmail",
                table: "StudioInvitations",
                columns: new[] { "SeriesId", "AssistantEmail" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions",
                sql: "\"Status\" IN ('Draft', 'Pending_EB_Review', 'Requires_Revision', 'EB_Rejected', 'EB_Approved', 'Conflict_Escalated')");
        }
    }
}
