using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDescriptionToPageTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskDescription",
                table: "PageTasks",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaskDescription",
                table: "PageTasks");
        }
    }
}
