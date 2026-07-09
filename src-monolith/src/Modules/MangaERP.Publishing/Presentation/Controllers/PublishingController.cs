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
    /// [EditorialBoard] Update publish schedule for an approved chapter.
    /// </summary>
    [HttpPatch("chapters/{chapterId:guid}/schedule")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(SchedulePublishResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateSchedule(Guid chapterId, [FromBody] UpdateScheduleRequest request, CancellationToken ct)
    {
        try
        {
            var command = new SchedulePublishCommand(
                ChapterId: chapterId,
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
    /// [EditorialBoard] Cancel publish schedule for an approved chapter.
    /// </summary>
    [HttpDelete("chapters/{chapterId:guid}/schedule")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CancelSchedule(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var command = new MangaERP.Publishing.Application.Commands.CancelSchedulePublish.CancelSchedulePublishCommand(chapterId);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
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

    /// <summary>
    /// [All roles] Get publication history for a series.
    /// </summary>
    [HttpGet("series/{seriesId}/history")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<MangaERP.Publishing.Application.Queries.GetPublicationHistory.PublicationRecordDto>), 200)]
    public async Task<IActionResult> GetPublicationHistory(Guid seriesId, CancellationToken ct)
    {
        var query = new MangaERP.Publishing.Application.Queries.GetPublicationHistory.GetPublicationHistoryQuery(seriesId);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [EditorialBoard, Admin] Get all approved chapters ready for publishing.
    /// </summary>
    [HttpGet("chapters/ready")]
    [Authorize(Roles = "EditorialBoard,Admin")]
    [ProducesResponseType(typeof(IEnumerable<MangaERP.Publishing.Application.Queries.GetReadyForPublish.ReadyForPublishChapterDto>), 200)]
    public async Task<IActionResult> GetReadyForPublish(CancellationToken ct)
    {
        var query = new MangaERP.Publishing.Application.Queries.GetReadyForPublish.GetReadyForPublishQuery();
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [EditorialBoard, Admin] Get publishing schedule (approved chapters with scheduled publish date).
    /// </summary>
    [HttpGet("schedule")]
    [Authorize(Roles = "EditorialBoard,Admin")]
    [ProducesResponseType(typeof(IEnumerable<MangaERP.Publishing.Application.Queries.GetPublishingSchedule.ScheduledChapterDto>), 200)]
    public async Task<IActionResult> GetPublishingSchedule(CancellationToken ct)
    {
        var query = new MangaERP.Publishing.Application.Queries.GetPublishingSchedule.GetPublishingScheduleQuery();
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}

public record ScheduleRequest(
    Guid ChapterId,
    Guid SeriesId,
    string IssueType,
    DateTime ScheduledPublishAt
);

public record PublishRequest(Guid ChapterId);

public record UpdateScheduleRequest(
    Guid SeriesId,
    string IssueType,
    DateTime ScheduledPublishAt
);
