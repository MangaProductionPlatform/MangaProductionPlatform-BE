using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPageTaskBaseImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseImageUrl",
                table: "PageTasks",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseImageUrl",
                table: "PageTasks");
        }
    }
}
