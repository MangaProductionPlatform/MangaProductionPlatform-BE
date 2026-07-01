using MangaERP.Api.Queries.GetAdminDashboard;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
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
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
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

        var result = await _mediator.Send(new GetAdminDashboardQuery(), ct);
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
}
