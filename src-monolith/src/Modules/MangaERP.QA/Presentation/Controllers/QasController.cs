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
    /// [TantouEditor] Send feedback batch, changing Chapter status to Rejected.
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
        var query = new GetBugPinsQuery(chapterId);
        var result = await _mediator.Send(query, ct);
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
    public async Task<IActionResult> ResolvePin(Guid pinId, CancellationToken ct)
    {
        try
        {
            var command = new ResolveBugPinCommand(pinId, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
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
        var query = new MangaERP.QA.Application.Queries.GetQASession.GetQASessionQuery(chapterId);
        var result = await _mediator.Send(query, ct);

        if (result == null) return NotFound(new { message = "QA Session not found." });
        return Ok(result);
    }
}

public record AddPinRequest(
    Guid PageTaskId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    string IssueType,
    Guid BatchToken
);

public record SendFeedbackRequest(Guid BatchToken);
