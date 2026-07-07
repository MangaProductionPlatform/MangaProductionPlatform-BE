using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Api.Controllers;

/// <summary>
/// Controller cung cấp báo cáo thống kê hiệu suất nội bộ Ban biên tập (EB/EiC).
/// Route: /api/v1/board/performance-reports
/// </summary>
[ApiController]
[Route("api/v1/board")]
[Authorize(Roles = "EditorialBoard,EditorInChief")]
public class BoardReportsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BoardReportsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// [EB / EiC] Báo cáo thống kê hiệu suất xét duyệt bản thảo của Ban biên tập.
    /// </summary>
    /// <remarks>
    /// Trả về:
    /// - Tỉ lệ Approve / Reject / Requires_Revision theo tổng số submissions đã xử lý
    /// - Số lượng submission đang pending / conflict
    /// - Thời gian xử lý trung bình (CreatedAt → ReviewedAt) tính bằng giờ
    /// - Phân bổ phiếu vote theo loại (APPROVE / REJECT / REQ_REVISION)
    /// - Top 5 tháng có số lượng submission nộp nhiều nhất (12 tháng gần nhất)
    /// </remarks>
    [HttpGet("performance-reports")]
    [ProducesResponseType(typeof(BoardReportsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetBoardReports(CancellationToken ct)
    {
        // ── 1. Phân bổ trạng thái tất cả submissions ──────────────────────────
        var statusBreakdown = await _db.SeriesSubmissions
            .GroupBy(s => s.Status)
            .Select(g => new StatusBreakdownDto
            {
                Status = g.Key.ToString(),
                Count  = g.Count()
            })
            .ToListAsync(ct);

        var totalResolved = statusBreakdown
            .Where(s => s.Status is
                nameof(SubmissionStatus.EB_Approved) or
                nameof(SubmissionStatus.EB_Rejected) or
                nameof(SubmissionStatus.Requires_Revision))
            .Sum(s => s.Count);

        var totalApproved  = statusBreakdown.FirstOrDefault(s => s.Status == nameof(SubmissionStatus.EB_Approved))?.Count  ?? 0;
        var totalRejected  = statusBreakdown.FirstOrDefault(s => s.Status == nameof(SubmissionStatus.EB_Rejected))?.Count  ?? 0;
        var totalRevisions = statusBreakdown.FirstOrDefault(s => s.Status == nameof(SubmissionStatus.Requires_Revision))?.Count ?? 0;
        var totalPending   = statusBreakdown.FirstOrDefault(s => s.Status == nameof(SubmissionStatus.Pending_EB_Review))?.Count ?? 0;
        var totalConflict  = statusBreakdown.FirstOrDefault(s => s.Status == nameof(SubmissionStatus.Conflict_Escalated))?.Count ?? 0;

        // ── 2. Thời gian xử lý trung bình (giờ) ──────────────────────────────
        // Pull timestamps to client, compute in memory (reviewed submissions only)
        var processingTimes = await _db.SeriesSubmissions
            .Where(s => s.ReviewedAt != null)
            .Select(s => new { s.CreatedAt, ReviewedAt = s.ReviewedAt!.Value })
            .ToListAsync(ct);

        double? avgProcessingHours = processingTimes.Count > 0
            ? processingTimes.Average(s => (s.ReviewedAt - s.CreatedAt).TotalHours)
            : null;

        // ── 3. Phân bổ phiếu vote ─────────────────────────────────────────────
        var voteBreakdown = await _db.SubmissionVotes
            .GroupBy(v => v.VoteType)
            .Select(g => new VoteBreakdownDto
            {
                VoteType = g.Key.ToString(),
                Count    = g.Count()
            })
            .ToListAsync(ct);

        // ── 4. Số submissions nộp theo tháng (12 tháng gần nhất) ─────────────
        var since = DateTime.UtcNow.AddMonths(-11);
        var submissionsPerMonth = await _db.SeriesSubmissions
            .Where(s => s.CreatedAt >= since)
            .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month })
            .Select(g => new MonthlySubmissionDto
            {
                Year  = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count()
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToListAsync(ct);

        // ── 5. Tỉ lệ phần trăm ───────────────────────────────────────────────
        double approveRate  = totalResolved > 0 ? Math.Round((double)totalApproved  / totalResolved * 100, 1) : 0;
        double rejectRate   = totalResolved > 0 ? Math.Round((double)totalRejected  / totalResolved * 100, 1) : 0;
        double revisionRate = totalResolved > 0 ? Math.Round((double)totalRevisions / totalResolved * 100, 1) : 0;

        return Ok(new BoardReportsDto
        {
            GeneratedAt         = DateTime.UtcNow,
            TotalSubmissions    = statusBreakdown.Sum(s => s.Count),
            TotalResolved       = totalResolved,
            TotalPending        = totalPending,
            TotalConflict       = totalConflict,
            ApproveRate         = approveRate,
            RejectRate          = rejectRate,
            RevisionRate        = revisionRate,
            AvgProcessingHours  = Math.Round(avgProcessingHours ?? 0, 1),
            StatusBreakdown     = statusBreakdown,
            VoteBreakdown       = voteBreakdown,
            SubmissionsPerMonth = submissionsPerMonth
        });
    }
}

// ── Response DTOs ──────────────────────────────────────────────────────────────

public record BoardReportsDto
{
    public DateTime GeneratedAt        { get; init; }

    // Tổng quan
    public int    TotalSubmissions    { get; init; }
    public int    TotalResolved       { get; init; }
    public int    TotalPending        { get; init; }
    public int    TotalConflict       { get; init; }

    // Tỉ lệ (%) trên tổng đã xử lý
    public double ApproveRate         { get; init; }
    public double RejectRate          { get; init; }
    public double RevisionRate        { get; init; }

    // Hiệu suất thời gian
    public double AvgProcessingHours  { get; init; }

    // Chi tiết
    public IEnumerable<StatusBreakdownDto>   StatusBreakdown     { get; init; } = [];
    public IEnumerable<VoteBreakdownDto>     VoteBreakdown       { get; init; } = [];
    public IEnumerable<MonthlySubmissionDto> SubmissionsPerMonth { get; init; } = [];
}

public record StatusBreakdownDto
{
    public string Status { get; init; } = string.Empty;
    public int    Count  { get; init; }
}

public record VoteBreakdownDto
{
    public string VoteType { get; init; } = string.Empty;
    public int    Count    { get; init; }
}

public record MonthlySubmissionDto
{
    public int Year  { get; init; }
    public int Month { get; init; }
    public int Count { get; init; }
}
