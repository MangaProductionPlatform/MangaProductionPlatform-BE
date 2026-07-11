using MediatR;
using MangaERP.QA.Application.Commands;
using MangaERP.QA.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.QA.Presentation.Controllers;

[ApiController]
[Route("api/v1/qa")]
public class QasController : ControllerBase
{
    private readonly IMediator _mediator;

    public QasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// [TantouEditor] Get chapters in ReadyForQA queue assigned to this editor.
    /// </summary>
    [HttpGet("queue")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(IEnumerable<MangaERP.QA.Application.Queries.GetQAQueue.QAQueueChapterDto>), 200)]
    public async Task<IActionResult> GetQAQueue(CancellationToken ct)
    {
        var query = new MangaERP.QA.Application.Queries.GetQAQueue.GetQAQueueQuery(GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Alias endpoint to resubmit a chapter for QA after fixing bugs.
    /// </summary>
    [HttpPost("chapters/{chapterId:guid}/resubmit")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(MangaERP.Chapter.Application.Commands.SubmitForQA.SubmitChapterForQAResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResubmitForQA(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var command = new MangaERP.Chapter.Application.Commands.SubmitForQA.SubmitChapterForQACommand(GetUserId(), chapterId);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Add a single bug pin to a chapter.
    /// </summary>
    [HttpPost("chapters/{chapterId:guid}/pins")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(Guid), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddPin(Guid chapterId, [FromBody] AddPinRequest request, CancellationToken ct)
    {
        try
        {
            var command = new AddBugPinCommand(
                ChapterId: chapterId,
                PageTaskId: request.PageTaskId,
                EditorId: GetUserId(),
                CoordinateX: request.CoordinateX,
                CoordinateY: request.CoordinateY,
                NoteMessage: request.NoteMessage,
                IssueType: request.IssueType,
                Severity: request.Severity ?? "Medium",
                Category: request.Category,
                BatchToken: request.BatchToken
            );

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Send feedback batch, changing Chapter status to QaRevisionRequired.
    /// </summary>
    [HttpPost("chapters/{chapterId:guid}/send-feedback")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(SendFeedbackBatchResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SendFeedback(Guid chapterId, [FromBody] SendFeedbackRequest request, CancellationToken ct)
    {
        try
        {
            var command = new SendFeedbackBatchCommand(chapterId, GetUserId(), request.BatchToken);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor, Mangaka] Get all bug pins of a chapter.
    /// </summary>
    [HttpGet("chapters/{chapterId:guid}/pins")]
    [Authorize(Roles = "TantouEditor,Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<BugPinDto>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetPins(Guid chapterId, CancellationToken ct)
    {
        var query = new GetBugPinsQuery(chapterId, GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Assistant, Mangaka] Get active bug pin for a specific page task.
    /// </summary>
    [HttpGet("tasks/{pageTaskId:guid}/qa-pin")]
    [Authorize(Roles = "Assistant,Mangaka")]
    [ProducesResponseType(typeof(BugPinDto), 200)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> GetPinByTask(Guid pageTaskId, CancellationToken ct)
    {
        var query = new MangaERP.QA.Application.Queries.GetBugPinByTaskQuery(pageTaskId, GetUserId());
        var result = await _mediator.Send(query, ct);
        if (result == null) return NoContent();
        return Ok(result);
    }

    /// <summary>
    /// [TantouEditor] Mark a specific bug pin as resolved.
    /// </summary>
    [HttpPost("pins/{pinId:guid}/resolve")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResolvePin(Guid pinId, [FromBody] ResolvePinRequest? request, CancellationToken ct)
    {
        try
        {
            var command = new ResolveBugPinCommand(pinId, GetUserId(), request?.Note, request?.ReviewedLayerId);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Mark a specific bug pin as unresolved / reopen.
    /// </summary>
    [HttpPost("pins/{pinId:guid}/unresolve")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UnresolvePin(Guid pinId, CancellationToken ct)
    {
        try
        {
            var command = new UnresolveBugPinCommand(pinId, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Assign an assistant to fix a bug pin.
    /// </summary>
    [HttpPost("pins/{pinId:guid}/assign-fix")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AssignFixTask(Guid pinId, [FromBody] AssignFixRequest request, CancellationToken ct)
    {
        try
        {
            var command = new MangaERP.QA.Application.Commands.AssignFixTask.AssignFixTaskCommand(
                pinId, GetUserId(), request.AssistantId, request.Instructions);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// [Assistant, Mangaka] Report a bug pin as fixed.
    /// </summary>
    [HttpPost("pins/{pinId:guid}/fixed")]
    [Authorize(Roles = "Assistant,Mangaka")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ReportBugPinFixed(Guid pinId, CancellationToken ct)
    {
        try
        {
            var command = new MangaERP.QA.Application.Commands.ReportBugPinFixed.ReportBugPinFixedCommand(pinId, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Approve a chapter if all bug pins are resolved.
    /// </summary>
    [HttpPost("chapters/{chapterId:guid}/approve")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(ApproveChapterResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ApproveChapter(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var command = new ApproveChapterCommand(chapterId, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor, Mangaka] Get the QA Session of a chapter.
    /// </summary>
    [HttpGet("chapters/{chapterId:guid}/session")]
    [Authorize(Roles = "TantouEditor,Mangaka")]
    [ProducesResponseType(typeof(MangaERP.QA.Application.Queries.GetQASession.QASessionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSession(Guid chapterId, CancellationToken ct)
    {
        var query = new MangaERP.QA.Application.Queries.GetQASession.GetQASessionQuery(chapterId, GetUserId());
        var result = await _mediator.Send(query, ct);

        if (result == null) return NotFound(new { message = "QA Session not found." });
        return Ok(result);
    }

    /// <summary>
    /// [TantouEditor, Mangaka] Get chapter pages with preview images for QA annotation.
    /// Authorizes via active QA session — not just AssignedEditorId.
    /// </summary>
    [HttpGet("chapters/{chapterId:guid}/pages")]
    [Authorize(Roles = "TantouEditor,Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<MangaERP.QA.Application.Queries.GetQAChapterPages.QAChapterPageDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetChapterPages(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var query = new MangaERP.QA.Application.Queries.GetQAChapterPages.GetQAChapterPagesQuery(chapterId, GetUserId());
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor, Mangaka] Get feedback batches for a chapter (pins grouped by batch token).
    /// </summary>
    [HttpGet("chapters/{chapterId:guid}/feedback")]
    [Authorize(Roles = "TantouEditor,Mangaka")]
    [ProducesResponseType(typeof(MangaERP.QA.Application.Queries.GetChapterFeedback.ChapterFeedbackDto), 200)]
    public async Task<IActionResult> GetFeedback(Guid chapterId, CancellationToken ct)
    {
        var query = new MangaERP.QA.Application.Queries.GetChapterFeedback.GetChapterFeedbackQuery(chapterId, GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [TantouEditor, Mangaka] Get QA summary (pin statistics and canApprove status).
    /// </summary>
    [HttpGet("chapters/{chapterId:guid}/summary")]
    [Authorize(Roles = "TantouEditor,Mangaka")]
    [ProducesResponseType(typeof(MangaERP.QA.Application.Queries.GetChapterQaSummary.ChapterQaSummaryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSummary(Guid chapterId, CancellationToken ct)
    {
        var query = new MangaERP.QA.Application.Queries.GetChapterQaSummary.GetChapterQaSummaryQuery(chapterId, GetUserId());
        var result = await _mediator.Send(query, ct);

        if (result == null) return NotFound(new { message = "No QA data found for this chapter." });
        return Ok(result);
    }

    /// <summary>
    /// [TantouEditor] Start a QA review session for a chapter (locks it for this editor).
    /// </summary>
    [HttpPost("chapters/{chapterId:guid}/start")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(MangaERP.QA.Application.Commands.StartQaReview.StartQaReviewResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> StartReview(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var command = new MangaERP.QA.Application.Commands.StartQaReview.StartQaReviewCommand(chapterId, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor, Mangaka] Get QA history for a chapter (sessions and bug pins).
    /// </summary>
    [HttpGet("chapters/{chapterId:guid}/history")]
    [Authorize(Roles = "TantouEditor,Mangaka")]
    [ProducesResponseType(typeof(MangaERP.QA.Application.Queries.GetQAHistory.QAHistoryDto), 200)]
    public async Task<IActionResult> GetHistory(Guid chapterId, CancellationToken ct)
    {
        var query = new MangaERP.QA.Application.Queries.GetQAHistory.GetQAHistoryQuery(chapterId, GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [TantouEditor] Reopen an Approved chapter back to ReadyForQA for a new review round.
    /// </summary>
    [HttpPost("chapters/{chapterId:guid}/reopen")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(MangaERP.QA.Application.Commands.ReopenChapter.ReopenChapterResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ReopenChapter(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var command = new MangaERP.QA.Application.Commands.ReopenChapter.ReopenChapterCommand(chapterId, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Update an existing open bug pin.
    /// </summary>
    [HttpPatch("pins/{pinId:guid}")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdatePin(Guid pinId, [FromBody] UpdatePinRequest request, CancellationToken ct)
    {
        try
        {
            var command = new MangaERP.QA.Application.Commands.UpdateBugPin.UpdateBugPinCommand(
                pinId, GetUserId(), request.NoteMessage, request.IssueType,
                request.CoordinateX, request.CoordinateY, request.Severity, request.Category);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Delete an existing open bug pin.
    /// </summary>
    [HttpDelete("pins/{pinId:guid}")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeletePin(Guid pinId, CancellationToken ct)
    {
        try
        {
            var command = new MangaERP.QA.Application.Commands.DeleteBugPin.DeleteBugPinCommand(pinId, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }
}

public record AddPinRequest(
    Guid PageTaskId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    string IssueType,
    string? Severity,
    string? Category,
    Guid BatchToken
);

public record SendFeedbackRequest(Guid BatchToken);

public record AssignFixRequest(Guid AssistantId, string? Instructions);

public record UpdatePinRequest(
    string? NoteMessage,
    string? IssueType,
    decimal? CoordinateX,
    decimal? CoordinateY,
    string? Severity,
    string? Category
);

public record ResolvePinRequest(string? Note, Guid? ReviewedLayerId);
