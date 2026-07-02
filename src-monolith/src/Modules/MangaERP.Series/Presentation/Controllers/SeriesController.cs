using MediatR;
using MangaERP.Series.Application.Queries.GetMySeries;
using MangaERP.Series.Application.Queries.GetSeriesDetail;
using MangaERP.Series.Application.Queries.GetAllSeries;
using MangaERP.Series.Application.Queries.GetCancellationQueue;
using MangaERP.Series.Application.Commands.RequestCancellation;
using MangaERP.Series.Application.Commands.ApproveCancellation;
using MangaERP.Series.Application.Commands.RejectCancellation;
using MangaERP.Series.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Series.Presentation.Controllers;

[ApiController]
[Route("api/v1/series")]
[Authorize]
public class SeriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeriesController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    private string GetUserRole()
        => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    // ── MANGAKA ───────────────────────────────────────────────────────────────

    /// <summary>
    /// [Mangaka] Lấy danh sách series của mình (đã được Editorial Board approve).
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<SeriesSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetMySeries(CancellationToken ct)
    {
        var query  = new GetMySeriesQuery(GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Gửi yêu cầu hủy series.
    /// Series phải đang Active hoặc Hiatus, chưa có pending cancellation request.
    /// </summary>
    /// <remarks>
    /// Body: { "reason": "Lý do hủy..." }
    /// </remarks>
    [HttpPost("{id:guid}/request-cancellation")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(RequestCancellationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RequestCancellation(
        Guid id, [FromBody] CancellationReasonRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RequestCancellationCommand(id, GetUserId(), request.Reason);
            var result  = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)        { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex)   { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex)           { return BadRequest(new { message = ex.Message }); }
    }

    // ── EDITORIAL BOARD / EIC ────────────────────────────────────────────────

    /// <summary>
    /// [EditorialBoard, EditorInChief] Lấy queue các series đang chờ duyệt yêu cầu hủy.
    /// </summary>
    [HttpGet("cancellation-queue")]
    [Authorize(Roles = "EditorialBoard,EditorInChief")]
    [ProducesResponseType(typeof(IEnumerable<CancellationQueueItemDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetCancellationQueue(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCancellationQueueQuery(), ct);
        return Ok(result);
    }

    // ── ADMIN ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// [Admin, EditorialBoard, EditorInChief] Xem toàn bộ series. Hỗ trợ filter theo status.
    /// Dùng cho AdminSeriesMonitoringPage.
    /// </summary>
    /// <remarks>GET /api/v1/series?status=Active|Hiatus|Cancelled</remarks>
    [HttpGet]
    [Authorize(Roles = "Admin,EditorialBoard,EditorInChief,TantouEditor")]
    [ProducesResponseType(typeof(IEnumerable<SeriesSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetAll(
        [FromQuery] SeriesStatus? status, CancellationToken ct)
    {
        var query  = new GetAllSeriesQuery(status);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [EditorialBoard, EditorInChief] Chấp thuận yêu cầu hủy series.
    /// Series sẽ chuyển sang trạng thái Cancelled.
    /// </summary>
    [HttpPost("{id:guid}/approve-cancellation")]
    [Authorize(Roles = "EditorialBoard,EditorInChief")]
    [ProducesResponseType(typeof(ApproveCancellationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ApproveCancellation(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new ApproveCancellationCommand(id, GetUserId());
            var result  = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// [EditorialBoard, EditorInChief] Từ chối yêu cầu hủy series.
    /// Series giữ nguyên trạng thái hiện tại (Active/Hiatus).
    /// </summary>
    /// <remarks>
    /// Body: { "reason": "Lý do từ chối..." }
    /// </remarks>
    [HttpPost("{id:guid}/reject-cancellation")]
    [Authorize(Roles = "EditorialBoard,EditorInChief")]
    [ProducesResponseType(typeof(RejectCancellationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RejectCancellation(
        Guid id, [FromBody] CancellationReasonRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RejectCancellationCommand(id, GetUserId(), request.Reason);
            var result  = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
    }

    // ── SHARED ────────────────────────────────────────────────────────────────

    /// <summary>
    /// [All authorized roles] Lấy chi tiết một series theo ID.
    /// Mangaka chỉ xem được series của mình. Staff xem được tất cả.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard,Admin")]
    [ProducesResponseType(typeof(SeriesDetailDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var query  = new GetSeriesDetailQuery(id, GetUserId(), GetUserRole());
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)        { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }
}

// ── Request Models ────────────────────────────────────────────────────────────

public record CancellationReasonRequest(string Reason);

