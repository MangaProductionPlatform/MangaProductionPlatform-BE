using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1CollaborationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE duplicate_count integer;
                DECLARE duplicate_ids text;
                BEGIN
                    SELECT count(*), string_agg("Id"::text, ',') INTO duplicate_count, duplicate_ids
                    FROM "StudioInvitations"
                    WHERE "Status" = 'Pending'
                      AND ("NormalizedAssistantEmail" IS NULL OR btrim("NormalizedAssistantEmail") = '');
                    IF duplicate_count > 0 THEN
                        RAISE EXCEPTION 'Phase1 preflight failed: % pending invitations have null/empty NormalizedAssistantEmail (IDs: %). Clean these rows before migration.', duplicate_count, duplicate_ids;
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "StudioInvitations"
                        WHERE "Status" = 'Pending'
                        GROUP BY "InviterMangakaId", "NormalizedAssistantEmail"
                        HAVING count(*) > 1
                    ) THEN
                        SELECT count(*), string_agg("Id"::text, ',') INTO duplicate_count, duplicate_ids
                        FROM "StudioInvitations" i
                        WHERE i."Status" = 'Pending'
                          AND EXISTS (SELECT 1 FROM "StudioInvitations" j
                                      WHERE j."Status" = 'Pending'
                                        AND j."InviterMangakaId" = i."InviterMangakaId"
                                        AND j."NormalizedAssistantEmail" = i."NormalizedAssistantEmail"
                                        AND j."Id" <> i."Id");
                        RAISE EXCEPTION 'Phase1 preflight failed: duplicate pending invitations (% affected rows; IDs: %) for the same Mangaka and NormalizedAssistantEmail. Clean before migration.', duplicate_count, duplicate_ids;
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_StudioInvitations_SeriesId_NormalizedAssistantEmail_Status",
                table: "StudioInvitations");

            migrationBuilder.CreateTable(
                name: "MangakaAssistantCollaborations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MangakaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SuspensionMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TerminatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MangakaAssistantCollaborations", x => x.Id);
                    table.CheckConstraint("CK_MangakaAssistantCollaborations_Ended", "(\"Status\" <> 'Ended') OR (\"EndedAt\" IS NOT NULL AND length(trim(\"EndReason\")) > 0 AND \"TerminatedByUserId\" IS NOT NULL)");
                    table.CheckConstraint("CK_MangakaAssistantCollaborations_Suspension", "(\"Status\" = 'Suspended' AND \"SuspensionMode\" IS NOT NULL) OR (\"Status\" <> 'Suspended' AND \"SuspensionMode\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_MangakaAssistantCollaborations_StudioInvitations_Invitation~",
                        column: x => x.InvitationId,
                        principalTable: "StudioInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MangakaAssistantCollaborations_Users_AssistantId",
                        column: x => x.AssistantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MangakaAssistantCollaborations_Users_MangakaId",
                        column: x => x.MangakaId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MangakaAssistantCollaborations_Users_TerminatedByUserId",
                        column: x => x.TerminatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollaborationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollaborationEvents_MangakaAssistantCollaborations_Collabor~",
                        column: x => x.CollaborationId,
                        principalTable: "MangakaAssistantCollaborations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudioInvitations_InviterMangakaId_NormalizedAssistantEmail~",
                table: "StudioInvitations",
                columns: new[] { "InviterMangakaId", "NormalizedAssistantEmail", "Status" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationEvents_ActorUserId",
                table: "CollaborationEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationEvents_CollaborationId_OccurredAt",
                table: "CollaborationEvents",
                columns: new[] { "CollaborationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MangakaAssistantCollaborations_AssistantId",
                table: "MangakaAssistantCollaborations",
                column: "AssistantId",
                unique: true,
                filter: "\"Status\" IN ('Active', 'Suspended', 'EndingRequested')");

            migrationBuilder.CreateIndex(
                name: "IX_MangakaAssistantCollaborations_InvitationId",
                table: "MangakaAssistantCollaborations",
                column: "InvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MangakaAssistantCollaborations_MangakaId_AssistantId",
                table: "MangakaAssistantCollaborations",
                columns: new[] { "MangakaId", "AssistantId" },
                unique: true,
                filter: "\"Status\" IN ('Active', 'Suspended', 'EndingRequested')");

            migrationBuilder.CreateIndex(
                name: "IX_MangakaAssistantCollaborations_TerminatedByUserId",
                table: "MangakaAssistantCollaborations",
                column: "TerminatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "CollaborationEvents" LIMIT 1)
                        OR EXISTS (SELECT 1 FROM "MangakaAssistantCollaborations" LIMIT 1) THEN
                        RAISE EXCEPTION 'Refusing destructive rollback: collaboration data exists. Export/archive it before rolling back Phase1CollaborationFoundation.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "CollaborationEvents");

            migrationBuilder.DropTable(
                name: "MangakaAssistantCollaborations");

            migrationBuilder.DropIndex(
                name: "IX_StudioInvitations_InviterMangakaId_NormalizedAssistantEmail~",
                table: "StudioInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_StudioInvitations_SeriesId_NormalizedAssistantEmail_Status",
                table: "StudioInvitations",
                columns: new[] { "SeriesId", "NormalizedAssistantEmail", "Status" },
                unique: true,
                filter: "\"Status\" = 'Pending'");
        }
    }
}
