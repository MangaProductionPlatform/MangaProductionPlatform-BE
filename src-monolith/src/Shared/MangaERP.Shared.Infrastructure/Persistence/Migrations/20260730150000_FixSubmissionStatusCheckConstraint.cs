using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSubmissionStatusCheckConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions",
                sql: "\"Status\" IN ('Draft', 'Pending_Tantou_Review', 'Tantou_Revision_Required', 'Pending_EB_Review', 'Requires_Revision', 'Editorial_Rejected_To_Tantou', 'Mangaka_Revision_Required', 'EB_Approved', 'EB_Rejected', 'Conflict_Escalated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeriesSubmissions_Status",
                table: "SeriesSubmissions",
                sql: "\"Status\" IN ('Draft', 'Pending_Tantou_Review', 'Tantou_Revision_Required', 'Pending_EB_Review', 'Editorial_Rejected_To_Tantou', 'Mangaka_Revision_Required', 'EB_Approved', 'Conflict_Escalated')");
        }
    }
}
