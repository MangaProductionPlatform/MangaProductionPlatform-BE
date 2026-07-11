using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeAndConstrainSubmissionStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "SeriesSubmissions"
                SET "Status" = CASE
                    WHEN "Status" IN ('Pending', 'UnderReview', 'Pending_TE_Review', 'RecommendedToBoard')
                        THEN 'Pending_EB_Review'
                    WHEN "Status" = 'RevisionRequired'
                        THEN 'Requires_Revision'
                    WHEN "Status" IN ('Rejected', 'TE_Rejected')
                        THEN 'EB_Rejected'
                    WHEN "Status" = 'Approved'
                        THEN 'EB_Approved'
                    ELSE "Status"
                END
                WHERE "Status" IN (
                    'Pending',
                    'UnderReview',
                    'Pending_TE_Review',
                    'RecommendedToBoard',
                    'RevisionRequired',
                    'Rejected',
                    'TE_Rejected',
                    'Approved'
                );
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions",
                sql: "\"Status\" IN ('Draft', 'Pending_EB_Review', 'Requires_Revision', 'EB_Rejected', 'EB_Approved', 'Conflict_Escalated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions");
        }
    }
}
