using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.QA.Application.Commands;
using MangaERP.QA.Domain.Entities;

namespace MangaERP.QA.Presentation.Controllers;

[ApiController]
[Route("api/v1/qa")]
[Authorize]
public class QAController : ControllerBase
{
    private readonly IMediator _mediator;

    public QAController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// MF3 Step 1-2: Get all bug pins for a chapter (for editor and mangaka views).
    /// </summary>
    [HttpGet("chapters/{chapterId:guid}/pins")]
    [Authorize(Roles = "TantouEditor,Mangaka")]
    public async Task<IActionResult> GetBugPins(Guid chapterId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBugPinsQuery(chapterId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// MF3 Step 3: Tantou Editor pins a visual/content issue on a page with % coordinates.
    /// </summary>
    [HttpPost("chapters/{chapterId:guid}/pins")]
    [Authorize(Roles = "TantouEditor")]
    public async Task<IActionResult> CreateBugPin(Guid chapterId, [FromBody] CreateBugPinRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateBugPinCommand(
                chapterId, request.PageTaskId, request.EditorId,
                request.CoordinateX, request.CoordinateY,
                request.NoteMessage, request.IssueType, request.BatchToken);

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// MF3 Step 5: Tantou Editor approves a chapter. Resolves all open pins and publishes ChapterApprovedEvent.
    /// </summary>
    [HttpPost("chapters/{chapterId:guid}/approve")]
    [Authorize(Roles = "TantouEditor")]
    public async Task<IActionResult> ApproveChapter(Guid chapterId, [FromQuery] Guid editorId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ApproveChapterQACommand(chapterId, editorId), cancellationToken);
        return NoContent();
    }
}

public record CreateBugPinRequest(
    Guid PageTaskId,
    Guid EditorId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    IssueType? IssueType,
    Guid BatchToken);
