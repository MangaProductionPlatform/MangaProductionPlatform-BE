using MangaERP.Chapter.Domain.Entities;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Api.Controllers;

[ApiController]
[Route("api/v1/board/dashboard")]
[Authorize(Roles = "EditorialBoard,EditorInChief,Admin")]
public class BoardDashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public BoardDashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);

        // 1. Overview
        var proposalsWaitingForVote = await _db.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Pending_EB_Review && !s.IsDeleted)
            .CountAsync(ct);

        var conflictsAwaitingResolution = await _db.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Conflict_Escalated && !s.IsDeleted)
            .CountAsync(ct);

        var chaptersReadyForPublish = await _db.Chapters
            .Where(c => c.Status == ChapterStatus.Approved && !c.IsDeleted)
            .CountAsync(ct);

        var scheduledPublicationsThisWeek = await _db.Chapters
            .Where(c => c.ScheduledPublishAt >= startOfWeek && c.ScheduledPublishAt < endOfWeek && c.Status != ChapterStatus.Published && !c.IsDeleted)
            .CountAsync(ct);

        var cancellationRequestsPending = await _db.MangaSeries
            .Where(s => s.CancellationStatus == CancellationRequestStatus.Pending && !s.IsDeleted)
            .CountAsync(ct);

        // 2. Proposal Queue
        var proposalQueue = await _db.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Pending_EB_Review && !s.IsDeleted)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new
            {
                id = s.Id,
                title = s.Title,
                submitterId = s.SubmitterId,
                submittedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        // 3. Publishing Queue
        var publishingQueue = await _db.Chapters
            .Where(c => c.Status == ChapterStatus.Approved && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                id = c.Id,
                seriesId = c.SeriesId,
                title = c.Title,
                chapterNumber = c.ChapterNumber,
                approvedAt = c.CreatedAt // We might not have ApprovedAt directly on Chapter, fallback to CreatedAt or fetch from QASession if needed
            })
            .ToListAsync(ct);

        // 4. Upcoming Schedule
        var upcomingSchedule = await _db.Chapters
            .Where(c => c.ScheduledPublishAt != null && c.Status != ChapterStatus.Published && !c.IsDeleted)
            .OrderBy(c => c.ScheduledPublishAt)
            .Take(10)
            .Select(c => new
            {
                id = c.Id,
                seriesId = c.SeriesId,
                title = c.Title,
                chapterNumber = c.ChapterNumber,
                scheduledPublishAt = c.ScheduledPublishAt
            })
            .ToListAsync(ct);

        // 5. Cancellation Queue
        var cancellationQueue = await _db.MangaSeries
            .Where(s => s.CancellationStatus == CancellationRequestStatus.Pending && !s.IsDeleted)
            .OrderBy(s => s.CancellationRequestedAt)
            .Select(s => new
            {
                id = s.Id,
                title = s.Title,
                requestedAt = s.CancellationRequestedAt,
                reason = s.CancellationReason
            })
            .ToListAsync(ct);

        // 6. Ranking Snapshot (Top 5)
        var rankingSnapshot = await _db.RankingSnapshots
            .OrderByDescending(r => r.Score)
            .Take(5)
            .Select(r => new
            {
                seriesId = r.SeriesId,
                score = r.Score,
                rank = r.Rank,
                calculatedAt = r.CreatedAt
            })
            .ToListAsync(ct);

        // 7. Recent Activity
        var recentActivity = await _db.SystemAuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(10)
            .Select(l => new { action = l.ActionName, description = l.Description, timestamp = l.Timestamp })
            .ToListAsync(ct);

        return Ok(new
        {
            overview = new
            {
                proposalsWaitingForVote,
                conflictsAwaitingResolution,
                chaptersReadyForPublish,
                scheduledPublicationsThisWeek,
                cancellationRequestsPending
            },
            proposalQueue,
            publishingQueue,
            upcomingSchedule,
            cancellationQueue,
            rankingSnapshot,
            recentActivity
        });
    }
}
