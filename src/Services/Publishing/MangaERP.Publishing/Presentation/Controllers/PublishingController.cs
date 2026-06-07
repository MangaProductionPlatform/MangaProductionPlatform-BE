using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Publishing.Application;

namespace MangaERP.Publishing.Presentation.Controllers;

[ApiController]
[Route("api/v1/publishing")]
[Authorize]
public class PublishingController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublishingController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// MF3 Step 6: Editorial Board selects issue type and schedules publish timestamp.
    /// </summary>
    [HttpPost("schedule")]
    [Authorize(Roles = "EditorialBoard")]
    public async Task<IActionResult> SchedulePublication([FromBody] SchedulePublicationCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// MF3 Step 7: Publish a chapter (called by Hangfire background job or manually for testing).
    /// </summary>
    [HttpPost("publish")]
    [Authorize(Roles = "EditorialBoard")]
    public async Task<IActionResult> PublishChapter([FromBody] PublishChapterCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>Get publication history for a series.</summary>
    [HttpGet("series/{seriesId:guid}/history")]
    public async Task<IActionResult> GetPublicationHistory(Guid seriesId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPublicationHistoryQuery(seriesId), cancellationToken);
        return Ok(result);
    }
}
