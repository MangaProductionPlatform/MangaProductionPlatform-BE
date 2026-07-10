using MangaERP.Chapter.Domain.Entities;
using MangaERP.QA.Domain.Entities;
using MangaERP.Series.Domain.Entities;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MangaERP.Api.Controllers;

/// <summary>
/// Dashboard nghiệp vụ cho Editor (TantouEditor làm việc 1-1 với Mangaka).
/// Route: /api/v1/editor/dashboard
/// </summary>
/// <remarks>
/// ⚠️ PHÂN TÁCH VAI TRÒ — KHÔNG THÊM "Admin" VÀO ĐÂY.
/// Admin chỉ quản trị kỹ thuật hệ thống (tài khoản, cấu hình, audit log).
/// Dashboard này chứa dữ liệu nghiệp vụ QA: QA queue, revision watchlist, bug pins,
/// assigned series — là luồng công việc riêng của TantouEditor và EditorInChief.
/// Admin không có context để xử lý các dữ liệu này.
/// Ref: IdentityEnums.cs — UserRole design notes.
/// </remarks>
[ApiController]
[Route("api/v1/editor/dashboard")]
[Authorize(Roles = "TantouEditor,EditorInChief")]
public class EditorDashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public EditorDashboardController(AppDbContext db)
    {
        _db = db;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var userId = GetUserId();
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // 1. Overview
        // Series assigned to this editor (Mangaka's ManagingTantouId == editor)
        var assignedSeriesCount = await _db.Set<User>()
            .Where(u => u.ManagingTantouId == userId)
            .Join(_db.MangaSeries, u => u.Id, s => s.AuthorId, (u, s) => s)
            .Where(s => !s.IsDeleted && s.Status == SeriesStatus.Active)
            .CountAsync(ct);

        var chaptersWaitingForQa = await _db.Chapters
            .Where(c => c.AssignedEditorId == userId && c.Status == ChapterStatus.ReadyForQA && !c.IsDeleted)
            .CountAsync(ct);

        var chaptersInRevision = await _db.Chapters
            .Where(c => c.AssignedEditorId == userId && c.Status == ChapterStatus.QaRevisionRequired && !c.IsDeleted)
            .CountAsync(ct);

        // Bug pins fixed by Mangaka, waiting for Editor to verify (Resolved)
        var pinsAwaitingVerification = await _db.BugPins
            .Where(p => p.EditorId == userId && p.Status == "Fixed")
            .CountAsync(ct);

        var approvedThisMonth = await _db.QASessions
            .Where(q => q.EditorId == userId && q.IsApproved && q.ApprovedAt >= startOfMonth)
            .CountAsync(ct);

        // 2. QA Queue
        var qaQueue = await _db.Chapters
            .Where(c => c.AssignedEditorId == userId && c.Status == ChapterStatus.ReadyForQA && !c.IsDeleted)
            .Select(c => new
            {
                id = c.Id,
                title = c.Title,
                chapterNumber = c.ChapterNumber,
                seriesId = c.SeriesId,
                submittedAt = c.CreatedAt
            })
            .ToListAsync(ct);

        // 3. Revision Watchlist (chapters in revision + pin counts)
        var revisionChapters = await _db.Chapters
            .Where(c => c.AssignedEditorId == userId && c.Status == ChapterStatus.QaRevisionRequired && !c.IsDeleted)
            .Select(c => new
            {
                id = c.Id,
                title = c.Title,
                chapterNumber = c.ChapterNumber,
                seriesId = c.SeriesId
            })
            .ToListAsync(ct);

        var revisionChapterIds = revisionChapters.Select(c => c.id).ToList();
        var pinCounts = await _db.BugPins
            .Where(p => revisionChapterIds.Contains(p.ChapterId))
            .GroupBy(p => p.ChapterId)
            .Select(g => new
            {
                ChapterId = g.Key,
                OpenCount = g.Count(p => p.Status == "Open"),
                InFixingCount = g.Count(p => p.Status == "InFixing"),
                FixedCount = g.Count(p => p.Status == "Fixed")
            })
            .ToDictionaryAsync(g => g.ChapterId, ct);

        var revisionWatchlist = revisionChapters.Select(c => new
            {
                id = c.id,
                title = c.title,
                chapterNumber = c.chapterNumber,
                seriesId = c.seriesId,
                pins = new
                {
                    open = pinCounts.ContainsKey(c.id) ? pinCounts[c.id].OpenCount : 0,
                    inFixing = pinCounts.ContainsKey(c.id) ? pinCounts[c.id].InFixingCount : 0,
                    @fixed = pinCounts.ContainsKey(c.id) ? pinCounts[c.id].FixedCount : 0
                }
            }).ToList();

        // 4. Upcoming Publishing
        var upcomingPublishing = await _db.Chapters
            .Where(c => c.AssignedEditorId == userId && c.ScheduledPublishAt != null && c.Status != ChapterStatus.Published && !c.IsDeleted)
            .OrderBy(c => c.ScheduledPublishAt)
            .Take(10)
            .Select(c => new
            {
                id = c.Id,
                title = c.Title,
                chapterNumber = c.ChapterNumber,
                seriesId = c.SeriesId,
                scheduledPublishAt = c.ScheduledPublishAt
            })
            .ToListAsync(ct);

        // 5. Recent Activity
        var recentActivity = await _db.SystemAuditLogs
            .Where(l => l.ActorId == userId)
            .OrderByDescending(l => l.Timestamp)
            .Take(10)
            .Select(l => new { action = l.ActionName, description = l.Description, timestamp = l.Timestamp })
            .ToListAsync(ct);

        return Ok(new
        {
            overview = new
            {
                assignedSeriesCount,
                chaptersWaitingForQa,
                chaptersInRevision,
                pinsAwaitingVerification,
                approvedThisMonth
            },
            qaQueue,
            revisionWatchlist,
            upcomingPublishing,
            recentActivity
        });
    }
}
