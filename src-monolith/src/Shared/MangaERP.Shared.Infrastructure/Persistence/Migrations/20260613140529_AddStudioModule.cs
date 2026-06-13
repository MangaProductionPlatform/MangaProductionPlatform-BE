using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MangaERP.Shared.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudioModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudioInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterMangakaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AssistantUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsNewAccountFlow = table.Column<bool>(type: "boolean", nullable: false),
                    ActivationToken = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioInvitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudioInvitations_AssistantUserId_Status",
                table: "StudioInvitations",
                columns: new[] { "AssistantUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudioInvitations_SeriesId_AssistantEmail",
                table: "StudioInvitations",
                columns: new[] { "SeriesId", "AssistantEmail" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudioInvitations");
        }
    }
}
