using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubmissionStateNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'Pending_TE_Review' WHERE \"Status\" IN ('Pending', 'UnderReview');");
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'Pending_EB_Review' WHERE \"Status\" = 'RecommendedToBoard';");
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'Requires_Revision' WHERE \"Status\" = 'RevisionRequired';");
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'TE_Rejected' WHERE \"Status\" = 'Rejected';");
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'EB_Approved' WHERE \"Status\" = 'Approved';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'Pending' WHERE \"Status\" = 'Pending_TE_Review';");
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'RecommendedToBoard' WHERE \"Status\" = 'Pending_EB_Review';");
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'RevisionRequired' WHERE \"Status\" = 'Requires_Revision';");
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'Rejected' WHERE \"Status\" IN ('TE_Rejected', 'EB_Rejected');");
            migrationBuilder.Sql("UPDATE \"SeriesSubmissions\" SET \"Status\" = 'Approved' WHERE \"Status\" = 'EB_Approved';");
        }
    }
}
