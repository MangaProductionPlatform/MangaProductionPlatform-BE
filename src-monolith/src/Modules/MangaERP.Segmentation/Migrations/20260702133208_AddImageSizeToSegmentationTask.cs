using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Segmentation.Migrations
{
    /// <inheritdoc />
    public partial class AddImageSizeToSegmentationTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalHeight",
                table: "SegmentationTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalWidth",
                table: "SegmentationTasks",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalHeight",
                table: "SegmentationTasks");

            migrationBuilder.DropColumn(
                name: "OriginalWidth",
                table: "SegmentationTasks");
        }
    }
}
