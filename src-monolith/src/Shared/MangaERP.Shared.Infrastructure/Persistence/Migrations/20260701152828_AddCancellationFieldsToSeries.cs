using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationFieldsToSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "MangaSeries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationRejectReason",
                table: "MangaSeries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationRequestedAt",
                table: "MangaSeries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancellationRequestedById",
                table: "MangaSeries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationReviewedAt",
                table: "MangaSeries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancellationReviewedById",
                table: "MangaSeries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancellationStatus",
                table: "MangaSeries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "MangaSeries");

            migrationBuilder.DropColumn(
                name: "CancellationRejectReason",
                table: "MangaSeries");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAt",
                table: "MangaSeries");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedById",
                table: "MangaSeries");

            migrationBuilder.DropColumn(
                name: "CancellationReviewedAt",
                table: "MangaSeries");

            migrationBuilder.DropColumn(
                name: "CancellationReviewedById",
                table: "MangaSeries");

            migrationBuilder.DropColumn(
                name: "CancellationStatus",
                table: "MangaSeries");
        }
    }
}
