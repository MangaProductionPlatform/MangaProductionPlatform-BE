using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.QA.Domain.Entities;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MangaERP.Api.Controllers;

/// <summary>
/// Dashboard & Analytics APIs cho Mangaka.
/// Route: /api/v1/mangaka
/// </summary>
[ApiController]
[Route("api/v1/mangaka")]
[Authorize(Roles = "Mangaka")]
public class MangakaDashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public MangakaDashboardController(AppDbContext db)
    {
        _db = db;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// API trả về chỉ số tổng quan Dashboard của Mangaka (Real EF Core queries).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var userId = GetUserId();
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // 1. Series của Mangaka
        var seriesList = await _db.MangaSeries
            .Where(s => s.AuthorId == userId && !s.IsDeleted)
            .ToListAsync(ct);

        var seriesIds = seriesList.Select(s => s.Id).ToList();
        var activeSeriesCount = seriesList.Count(s => s.Status == SeriesStatus.Active);

        // 2. Submissions đang chờ duyệt hoặc phản hồi
        var pendingSubmissionsCount = await _db.SeriesSubmissions
            .Where(s => s.SubmitterId == userId && !s.IsDeleted &&
                        (s.Status == SubmissionStatus.Draft ||
                         s.Status == SubmissionStatus.Pending_EB_Review ||
                         s.Status == SubmissionStatus.Requires_Revision ||
                         s.Status == SubmissionStatus.Conflict_Escalated))
            .CountAsync(ct);

        // 3. Danh sách Chapter thuộc các Series của Mangaka
        var chapters = await _db.Chapters
            .Where(c => seriesIds.Contains(c.SeriesId) && !c.IsDeleted)
            .ToListAsync(ct);

        var chapterIds = chapters.Select(c => c.Id).ToList();

        // 4. BugPins chưa giải quyết (QA feedback từ Editor)
        var unresolvedQAPins = await _db.BugPins
            .Where(p => chapterIds.Contains(p.ChapterId) && p.Status != "Resolved")
            .CountAsync(ct);

        // 5. Chapters đang trong quá trình sản xuất / sửa đổi
        var chaptersInProduction = chapters
            .Count(c => c.Status == ChapterStatus.Draft ||
                        c.Status == ChapterStatus.ReadyForQA ||
                        c.Status == ChapterStatus.QaRevisionRequired ||
                        c.Status == ChapterStatus.MangakaRevisionRequired ||
                        c.Status == ChapterStatus.PendingEditorialReview);

        // 6. Số trang hoàn thành trong tháng này
        var completedPagesThisMonth = await _db.PageTasks
            .Where(p => chapterIds.Contains(p.ChapterId) && !p.IsDeleted &&
                        p.TaskStatus == PageTaskStatus.Approved &&
                        p.UpdatedAt >= startOfMonth)
            .CountAsync(ct);

        // 7. Thông báo gần đây
        var recentNotifications = await _db.Notifications
            .Where(n => n.ReceiverId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new
            {
                id = n.Id,
                message = n.Message,
                date = n.CreatedAt,
                isRead = n.IsRead
            })
            .ToListAsync(ct);

        // 8. Thống kê theo Series (Lượt vote thực tế)
        var voteCounts = await _db.VoteData
            .Where(v => seriesIds.Contains(v.SeriesId))
            .GroupBy(v => v.SeriesId)
            .Select(g => new { SeriesId = g.Key, VoteCount = g.Count() })
            .ToDictionaryAsync(g => g.SeriesId, g => g.VoteCount, ct);

        var seriesAnalytics = seriesList.Select(s => new
        {
            seriesId = s.Id,
            title = s.Title,
            status = s.Status.ToString(),
            votes = voteCounts.GetValueOrDefault(s.Id, 0)
        }).ToList();

        return Ok(new
        {
            overview = new
            {
                activeSeriesCount,
                pendingSubmissionsCount,
                unresolvedQAPins,
                chaptersInProduction,
                completedPagesThisMonth
            },
            recentNotifications,
            seriesAnalytics
        });
    }

    /// <summary>
    /// API cung cấp dữ liệu Time-series cho các biểu đồ đường (Line charts) trên Dashboard Mangaka.
    /// </summary>
    [HttpGet("dashboard/analytics")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] Guid? seriesId = null,
        [FromQuery] int days = 14,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var daysCount = Math.Clamp(days, 1, 90);
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-daysCount + 1);

        // 1. Xác định các Series thuộc quyền của Mangaka
        var seriesQuery = _db.MangaSeries.Where(s => s.AuthorId == userId && !s.IsDeleted);
        if (seriesId.HasValue && seriesId.Value != Guid.Empty)
        {
            seriesQuery = seriesQuery.Where(s => s.Id == seriesId.Value);
        }

        var seriesIds = await seriesQuery.Select(s => s.Id).ToListAsync(ct);

        // 2. Các Chapter thuộc danh sách Series
        var chapters = await _db.Chapters
            .Where(c => seriesIds.Contains(c.SeriesId) && !c.IsDeleted)
            .ToListAsync(ct);

        var chapterIds = chapters.Select(c => c.Id).ToList();

        // 3. Tải danh sách PageTask của các Chapter
        var pageTasks = await _db.PageTasks
            .Where(p => chapterIds.Contains(p.ChapterId) && !p.IsDeleted)
            .ToListAsync(ct);

        var pageTaskIds = pageTasks.Select(p => p.Id).ToList();

        // ── A. Pages completed over time vs Target pages ──────────────────────
        var approvedPagesByDate = pageTasks
            .Where(p => p.TaskStatus == PageTaskStatus.Approved && p.UpdatedAt.Date >= startDate)
            .GroupBy(p => p.UpdatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var totalTargetPages = chapters.Sum(c => c.TotalPages);
        double dailyTargetRate = totalTargetPages > 0 ? (double)totalTargetPages / Math.Max(daysCount, 30) : 1.0;

        var pagesTrend = new List<object>();
        int cumulativeCompleted = pageTasks.Count(p => p.TaskStatus == PageTaskStatus.Approved && p.UpdatedAt.Date < startDate);
        double cumulativeTarget = cumulativeCompleted;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            int completedToday = approvedPagesByDate.GetValueOrDefault(date, 0);
            cumulativeCompleted += completedToday;
            cumulativeTarget += dailyTargetRate;

            pagesTrend.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                completedPages = completedToday,
                cumulativeCompletedPages = cumulativeCompleted,
                targetPages = Math.Round(cumulativeTarget, 1)
            });
        }

        // ── B. Tasks completed over time ──────────────────────────────────────
        var completedTasksByDate = pageTasks
            .Where(p => (p.TaskStatus == PageTaskStatus.Approved || p.TaskStatus == PageTaskStatus.Reviewing) && p.UpdatedAt.Date >= startDate)
            .GroupBy(p => p.UpdatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var tasksTrend = new List<object>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            tasksTrend.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                completedTasks = completedTasksByDate.GetValueOrDefault(date, 0)
            });
        }

        // ── C. Revision count over time (BugPins + Layer Rejections) ──────────
        var bugPinsByDate = await _db.BugPins
            .Where(b => chapterIds.Contains(b.ChapterId) && b.CreatedAt.Date >= startDate)
            .GroupBy(b => b.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Count, ct);

        var layerRejectionsByDate = await _db.ArtworkLayers
            .Where(l => pageTaskIds.Contains(l.PageTaskId) && l.RejectionNote != null && l.ReviewedAt.HasValue && l.ReviewedAt.Value.Date >= startDate)
            .GroupBy(l => l.ReviewedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Date, g => g.Count, ct);

        var revisionsTrend = new List<object>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            int bugPins = bugPinsByDate.GetValueOrDefault(date, 0);
            int layerRejections = layerRejectionsByDate.GetValueOrDefault(date, 0);

            revisionsTrend.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                bugPinsCount = bugPins,
                layerRejectionsCount = layerRejections,
                totalRevisions = bugPins + layerRejections
            });
        }

        // ── D. Chapter progress over time (% hoàn thành từng chapter) ─────────
        var chapterProgressTrend = chapters
            .Where(c => c.Status != ChapterStatus.Published && c.Status != ChapterStatus.Archived)
            .Select(c =>
            {
                var tasks = pageTasks.Where(p => p.ChapterId == c.Id).ToList();
                int approvedCount = tasks.Count(p => p.TaskStatus == PageTaskStatus.Approved);
                int totalPages = c.TotalPages > 0 ? c.TotalPages : tasks.Count;
                double progressPercent = totalPages > 0 ? Math.Round((double)approvedCount / totalPages * 100, 1) : 0;

                return new
                {
                    chapterId = c.Id,
                    seriesId = c.SeriesId,
                    title = c.Title,
                    chapterNumber = c.ChapterNumber,
                    status = c.Status.ToString(),
                    totalPages = totalPages,
                    approvedPages = approvedCount,
                    progressPercent = progressPercent
                };
            })
            .OrderBy(c => c.chapterNumber)
            .ToList();

        // ── E. Deadline risk trend (Task trễ / sắp quá hạn theo từng ngày) ─────
        var deadlineRiskTrend = new List<object>();
        var now = DateTime.UtcNow;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var endOfDate = date.AddDays(1).AddTicks(-1);

            int overdueCount = pageTasks.Count(p =>
                p.Deadline.HasValue &&
                p.Deadline.Value < endOfDate &&
                p.TaskStatus != PageTaskStatus.Approved);

            int nearDeadlineCount = pageTasks.Count(p =>
                p.Deadline.HasValue &&
                p.Deadline.Value >= date &&
                p.Deadline.Value <= date.AddDays(2) &&
                p.TaskStatus != PageTaskStatus.Approved);

            deadlineRiskTrend.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                overdueTasks = overdueCount,
                nearDeadlineTasks = nearDeadlineCount
            });
        }

        return Ok(new
        {
            timeframe = new { startDate = startDate.ToString("yyyy-MM-dd"), endDate = endDate.ToString("yyyy-MM-dd"), days = daysCount },
            pagesTrend,
            tasksTrend,
            revisionsTrend,
            chapterProgressTrend,
            deadlineRiskTrend
        });
    }
}

