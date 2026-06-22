using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionFeedbackPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetUrl",
                table: "Notifications",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubmissionFeedbackPins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageIdentifier = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CoordinateX = table.Column<double>(type: "numeric(5,2)", nullable: false),
                    CoordinateY = table.Column<double>(type: "numeric(5,2)", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionFeedbackPins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionFeedbackPins_SeriesSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "SeriesSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionFeedbackPins_SubmissionId_IsArchived",
                table: "SubmissionFeedbackPins",
                columns: new[] { "SubmissionId", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubmissionFeedbackPins");

            migrationBuilder.DropColumn(
                name: "TargetUrl",
                table: "Notifications");
        }
    }
}
