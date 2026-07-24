using MangaERP.Api.Queries.GetAdminDashboard;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MangaERP.Api.Controllers;

public class UpdateSamConfigDto
{
    public string Url { get; set; } = string.Empty;
    public string InternalApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Controller xử lý các endpoint Admin cần cross-module data.
/// Đặt ở Api layer (Composition Root) để hợp lệ inject nhiều module repositories.
/// Route: /api/v1/admin/dashboard, /api/v1/admin/workflow-stats
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly IMediator    _mediator;
    private readonly ILogger<AdminDashboardController> _logger;
    private readonly AppDbContext _db;

    public AdminDashboardController(
        IMediator mediator,
        ILogger<AdminDashboardController> logger,
        AppDbContext db)
    {
        _mediator = mediator;
        _logger   = logger;
        _db       = db;
    }

    /// <summary>
    /// [Admin] Tổng quan hệ thống: số lượng user theo role/status,
    /// submissions theo trạng thái, và series theo trạng thái.
    /// </summary>
    /// <remarks>
    /// **Response:**
    /// ```json
    /// {
    ///   "userStats": { "totalUsers": 25, "totalMangaka": 10, ... },
    ///   "submissionStats": { "pendingEBReview": 3, "conflictEscalated": 1, ... },
    ///   "seriesStats": { "active": 8, "pendingCancellationRequests": 2, ... },
    ///   "generatedAt": "2026-07-01T..."
    /// }
    /// ```
    /// </remarks>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken ct)
    {
        // Guardrail 1.2: Audit log bắt buộc cho endpoint xem toàn hệ thống
        var actorId = Guid.TryParse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (Guid?)null;

        var auditLog = new SystemAuditLog
        {
            ActorId     = actorId,
            ActionName  = "GET /api/v1/admin/dashboard",
            EntityType  = "System",
            Description = $"Admin dashboard viewed by userId={actorId}",
            IpAddress   = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Timestamp   = DateTime.UtcNow
        };
        await _db.SystemAuditLogs.AddAsync(auditLog, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[AUDIT] Admin dashboard accessed by {ActorId} from {Ip} at {Time}",
            actorId, auditLog.IpAddress, auditLog.Timestamp);

        var result = await _mediator.Send(new GetAdminDashboardQuery(startDate, endDate), ct);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Cập nhật SAM Service URL và API key runtime.
    /// </summary>
    [HttpPatch("sam-config")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> UpdateSamConfig([FromBody] UpdateSamConfigDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Url))
        {
            return BadRequest(new { message = "SAM URL is required." });
        }

        if (!Uri.TryCreate(dto.Url, UriKind.Absolute, out _))
        {
            return BadRequest(new { message = "Invalid SAM URL format." });
        }

        var actorId = Guid.TryParse(
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (Guid?)null;

        // Update URL
        var urlConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "SamService:Url", ct);
        if (urlConfig == null)
        {
            urlConfig = new MangaERP.Shared.Domain.Entities.SystemConfig { Key = "SamService:Url", Value = dto.Url.TrimEnd('/') };
            await _db.SystemConfigs.AddAsync(urlConfig, ct);
        }
        else
        {
            urlConfig.Value = dto.Url.TrimEnd('/');
        }

        // Update Api Key
        var apiKeyConfig = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "SamService:InternalApiKey", ct);
        if (apiKeyConfig == null)
        {
            apiKeyConfig = new MangaERP.Shared.Domain.Entities.SystemConfig { Key = "SamService:InternalApiKey", Value = dto.InternalApiKey };
            await _db.SystemConfigs.AddAsync(apiKeyConfig, ct);
        }
        else
        {
            apiKeyConfig.Value = dto.InternalApiKey;
        }

        // Guardrail 1.3/1.2: Audit Log (sensitive data like internalApiKey is NOT logged)
        var auditLog = new SystemAuditLog
        {
            ActorId     = actorId,
            ActionName  = "PATCH /api/v1/admin/sam-config",
            EntityType  = "SystemConfig",
            Description = $"SAM service configuration updated by admin. New URL: {dto.Url.TrimEnd('/')}",
            IpAddress   = HttpContext.Connection.RemoteIpAddress?.ToString(),
            Timestamp   = DateTime.UtcNow
        };
        await _db.SystemAuditLogs.AddAsync(auditLog, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "[AUDIT] SAM Service Configuration updated by {ActorId} from {Ip}. URL={Url}",
            actorId, auditLog.IpAddress, dto.Url.TrimEnd('/'));

        return Ok(new { message = "SAM configuration updated successfully." });
    }

    /// <summary>
    /// [Admin] Thống kê hiệu suất vận hành luồng nghiệp vụ:
    /// số chapter đang ở từng trạng thái QA, số task tồn đọng, số bản thảo pending...
    /// </summary>
    [HttpGet("workflow-stats")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetWorkflowStats(CancellationToken ct)
    {
        // Tổng hợp nhanh từ DB trực tiếp — không cần qua từng module
        var submissionStats = await _db.SeriesSubmissions
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var chapterStats = await _db.Chapters
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        var taskStats = await _db.PageTasks
            .GroupBy(t => t.TaskStatus)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);

        return Ok(new
        {
            generatedAt      = DateTime.UtcNow,
            submissionStats,
            chapterStats,
            taskStats
        });
    }

    /// <summary>
    /// [Admin] Lấy cấu hình hệ thống chung (settings).
    /// </summary>
    [HttpGet("settings")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IDictionary<string, string>), 200)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var configs = await _db.SystemConfigs.ToListAsync(ct);
        var result = configs.ToDictionary(c => c.Key, c => c.Value);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Cập nhật cấu hình hệ thống chung (bulk patch).
    /// </summary>
    [HttpPatch("settings")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> PatchSettings([FromBody] Dictionary<string, string> settings, CancellationToken ct)
    {
        if (settings == null || settings.Count == 0) return BadRequest("Settings required.");

        var existingConfigs = await _db.SystemConfigs.ToListAsync(ct);
        
        foreach (var kvp in settings)
        {
            var config = existingConfigs.FirstOrDefault(c => c.Key == kvp.Key);
            if (config == null)
            {
                config = new MangaERP.Shared.Domain.Entities.SystemConfig { Key = kvp.Key, Value = kvp.Value };
                _db.SystemConfigs.Add(config);
            }
            else
            {
                config.Value = kvp.Value;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Settings updated successfully." });
    }

    /// <summary>
    /// [Admin] Danh sách static roles của hệ thống — dùng cho FE dropdown khi provision account.
    /// </summary>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public IActionResult GetRoles()
    {
        var roles = new[]
        {
            new { value = (int)UserRole.Admin,          name = nameof(UserRole.Admin),          description = "Quản trị hệ thống kỹ thuật", provisionable = false },
            new { value = (int)UserRole.EditorialBoard, name = nameof(UserRole.EditorialBoard), description = "Ban biên tập - duyệt bản thảo & xuất bản", provisionable = true },
            new { value = (int)UserRole.TantouEditor,   name = nameof(UserRole.TantouEditor),   description = "Biên tập viên phụ trách 1-1 với Mangaka", provisionable = true },
            new { value = (int)UserRole.Mangaka,        name = nameof(UserRole.Mangaka),        description = "Tác giả manga", provisionable = true },
            new { value = (int)UserRole.Assistant,      name = nameof(UserRole.Assistant),      description = "Trợ lý vẽ", provisionable = true },
            new { value = (int)UserRole.EditorInChief,  name = nameof(UserRole.EditorInChief),  description = "Tổng biên tập - phân xử xung đột", provisionable = true },
        };

        return Ok(new { roles });
    }

    /// <summary>
    /// [Admin] One-time data migration: chuyển tất cả submissions đang
    /// Pending_Tantou_Review sang Pending_EB_Review và tạo EditorialReviewAssignment
    /// slots cho chúng.
    /// Dùng để fix dữ liệu cũ từ trước khi refactor MF1 (Tantou không còn tham gia
    /// duyệt series submission nữa).
    /// Idempotent: chạy nhiều lần vẫn an toàn.
    /// </summary>
    [HttpPost("migrate-tantou-submissions")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> MigrateTantouSubmissions(CancellationToken ct)
    {
        var stuckSubmissions = await _db.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Pending_Tantou_Review)
            .ToListAsync(ct);

        if (stuckSubmissions.Count == 0)
            return Ok(new { migrated = 0, message = "No stuck Pending_Tantou_Review submissions found." });

        // Lấy 2 EB đầu tiên để assign slots (idempotent — sẽ skip nếu đã có assignment)
        var ebReviewers = await _db.Users
            .Where(u => u.AccountStatus == AccountStatus.Active && !u.IsDeleted &&
                (u.Role == UserRole.EditorialBoard ||
                 u.UserRoles.Any(ur => ur.Role.Name == RoleNames.EditorialBoard)))
            .OrderBy(u => u.Id)
            .Take(2)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (ebReviewers.Count < 2)
            return BadRequest(new { error = "Cần ít nhất 2 EditorialBoard members đang active để tạo review slots." });

        int migrated = 0;
        foreach (var submission in stuckSubmissions)
        {
            // Chuyển status sang Pending_EB_Review
            submission.PromoteToPendingEBReview();

            // Tạo review slots nếu chưa có
            var existingSlots = await _db.EditorialReviewAssignments
                .Where(a => a.WorkType == EditorialWorkType.SeriesSubmission
                    && a.WorkId == submission.Id
                    && a.RoundNumber == submission.CurrentRound)
                .CountAsync(ct);

            if (existingSlots < 2)
            {
                if (existingSlots == 0)
                {
                    _db.EditorialReviewAssignments.Add(
                        EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submission.Id, submission.CurrentRound, ebReviewers[0]));
                    _db.EditorialReviewAssignments.Add(
                        EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submission.Id, submission.CurrentRound, ebReviewers[1]));
                }
                else
                {
                    // Chỉ còn thiếu 1 slot
                    var takenBy = await _db.EditorialReviewAssignments
                        .Where(a => a.WorkType == EditorialWorkType.SeriesSubmission
                            && a.WorkId == submission.Id
                            && a.RoundNumber == submission.CurrentRound)
                        .Select(a => a.ReviewerId)
                        .FirstAsync(ct);
                    var secondReviewer = ebReviewers.First(id => id != takenBy);
                    _db.EditorialReviewAssignments.Add(
                        EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submission.Id, submission.CurrentRound, secondReviewer));
                }
            }

            migrated++;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { migrated, message = $"{migrated} submission(s) migrated from Pending_Tantou_Review to Pending_EB_Review." });
    }

    /// <summary>
    /// [Admin] Biểu đồ xu hướng (Bản thảo Submissions & Series truyện) theo thời gian.
    /// Default range: 30 ngày gần nhất nếu không truyền.
    /// groupBy: "day" (default) hoặc "month".
    /// </summary>
    [HttpGet("charts")]
    [ProducesResponseType(typeof(AdminChartsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetCharts(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string groupBy = "day",
        CancellationToken ct = default)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var isMonthly = string.Equals(groupBy, "month", StringComparison.OrdinalIgnoreCase);

        // 1. Submissions trend
        var submissionsQuery = _db.SeriesSubmissions.AsNoTracking()
            .Where(s => s.CreatedAt >= start && s.CreatedAt <= end);

        List<TrendDataPointDto> submissionTrends;
        if (isMonthly)
        {
            var raw = await submissionsQuery
                .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync(ct);

            submissionTrends = raw
                .Select(x => new TrendDataPointDto($"{x.Year:D4}-{x.Month:D2}", x.Count))
                .OrderBy(x => x.Date)
                .ToList();
        }
        else
        {
            var raw = await submissionsQuery
                .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month, s.CreatedAt.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
                .ToListAsync(ct);

            submissionTrends = raw
                .Select(x => new TrendDataPointDto($"{x.Year:D4}-{x.Month:D2}-{x.Day:D2}", x.Count))
                .OrderBy(x => x.Date)
                .ToList();
        }

        // 2. Series trend
        var seriesQuery = _db.MangaSeries.AsNoTracking()
            .Where(s => s.CreatedAt >= start && s.CreatedAt <= end);

        List<TrendDataPointDto> seriesTrends;
        if (isMonthly)
        {
            var raw = await seriesQuery
                .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync(ct);

            seriesTrends = raw
                .Select(x => new TrendDataPointDto($"{x.Year:D4}-{x.Month:D2}", x.Count))
                .OrderBy(x => x.Date)
                .ToList();
        }
        else
        {
            var raw = await seriesQuery
                .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month, s.CreatedAt.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
                .ToListAsync(ct);

            seriesTrends = raw
                .Select(x => new TrendDataPointDto($"{x.Year:D4}-{x.Month:D2}-{x.Day:D2}", x.Count))
                .OrderBy(x => x.Date)
                .ToList();
        }

        return Ok(new AdminChartsDto(
            submissionTrends,
            seriesTrends,
            DateTime.UtcNow
        ));
    }
}

public record AdminChartsDto(
    IEnumerable<TrendDataPointDto> SubmissionTrends,
    IEnumerable<TrendDataPointDto> SeriesTrends,
    DateTime GeneratedAt
);

public record TrendDataPointDto(
    string Date,
    int Count
);
