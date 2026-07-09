using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModelsForGlobalQueryFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RankingSnapshots_SeriesId_VotePeriod",
                table: "RankingSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_RankingSnapshots_VotePeriod_Rank",
                table: "RankingSnapshots");

            migrationBuilder.RenameColumn(
                name: "VotePeriod",
                table: "RankingSnapshots",
                newName: "Period");

            migrationBuilder.RenameColumn(
                name: "TotalVotes",
                table: "RankingSnapshots",
                newName: "Views");

            migrationBuilder.AddColumn<int>(
                name: "Comments",
                table: "RankingSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Favorites",
                table: "RankingSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Likes",
                table: "RankingSnapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Score",
                table: "RankingSnapshots",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotDate",
                table: "RankingSnapshots",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "TrendScore",
                table: "RankingSnapshots",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "BugPins",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "BugPins",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RankingSnapshots_Period_Rank_SnapshotDate",
                table: "RankingSnapshots",
                columns: new[] { "Period", "Rank", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RankingSnapshots_SeriesId_Period_SnapshotDate",
                table: "RankingSnapshots",
                columns: new[] { "SeriesId", "Period", "SnapshotDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RankingSnapshots_Period_Rank_SnapshotDate",
                table: "RankingSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_RankingSnapshots_SeriesId_Period_SnapshotDate",
                table: "RankingSnapshots");

            migrationBuilder.DropColumn(
                name: "Comments",
                table: "RankingSnapshots");

            migrationBuilder.DropColumn(
                name: "Favorites",
                table: "RankingSnapshots");

            migrationBuilder.DropColumn(
                name: "Likes",
                table: "RankingSnapshots");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "RankingSnapshots");

            migrationBuilder.DropColumn(
                name: "SnapshotDate",
                table: "RankingSnapshots");

            migrationBuilder.DropColumn(
                name: "TrendScore",
                table: "RankingSnapshots");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "BugPins");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "BugPins");

            migrationBuilder.RenameColumn(
                name: "Views",
                table: "RankingSnapshots",
                newName: "TotalVotes");

            migrationBuilder.RenameColumn(
                name: "Period",
                table: "RankingSnapshots",
                newName: "VotePeriod");

            migrationBuilder.CreateIndex(
                name: "IX_RankingSnapshots_SeriesId_VotePeriod",
                table: "RankingSnapshots",
                columns: new[] { "SeriesId", "VotePeriod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankingSnapshots_VotePeriod_Rank",
                table: "RankingSnapshots",
                columns: new[] { "VotePeriod", "Rank" });
        }
    }
}
