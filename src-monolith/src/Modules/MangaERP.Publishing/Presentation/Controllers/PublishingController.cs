using MediatR;
using MangaERP.Publishing.Application.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Publishing.Presentation.Controllers;

[ApiController]
[Route("api/v1/publishing")]
public class PublishingController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublishingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// [EditorialBoard] Set publish schedule for an approved chapter.
    /// </summary>
    [HttpPost("schedule")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(SchedulePublishResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ScheduleChapter([FromBody] ScheduleRequest request, CancellationToken ct)
    {
        try
        {
            var command = new SchedulePublishCommand(
                ChapterId: request.ChapterId,
                SeriesId: request.SeriesId,
                IssueType: request.IssueType,
                ScheduledPublishAt: request.ScheduledPublishAt
            );

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// [EditorialBoard] Publish an approved chapter immediately.
    /// </summary>
    [HttpPost("publish")]
    [Authorize(Roles = "EditorialBoard,Admin")]
    [ProducesResponseType(typeof(PublishChapterResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> PublishChapter([FromBody] PublishRequest request, CancellationToken ct)
    {
        try
        {
            var command = new PublishChapterCommand(request.ChapterId, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }
}

public record ScheduleRequest(
    Guid ChapterId,
    Guid SeriesId,
    string IssueType,
    DateTime ScheduledPublishAt
);

public record PublishRequest(Guid ChapterId);
