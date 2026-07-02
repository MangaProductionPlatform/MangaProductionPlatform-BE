using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Segmentation.Migrations
{
    /// <inheritdoc />
    public partial class AddSegmentationTaskTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SegmentationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaskRle = table.Column<string>(type: "text", nullable: false),
                    Bbox = table.Column<int[]>(type: "integer[]", nullable: false),
                    TaskType = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToUserRole = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentationTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SegmentationTasks_AssignedToUserId",
                table: "SegmentationTasks",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SegmentationTasks_Status",
                table: "SegmentationTasks",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SegmentationTasks");
        }
    }
}
