using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageTaskRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegionMask",
                table: "PageTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "PageTasks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "General");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegionMask",
                table: "PageTasks");

            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "PageTasks");
        }
    }
}
