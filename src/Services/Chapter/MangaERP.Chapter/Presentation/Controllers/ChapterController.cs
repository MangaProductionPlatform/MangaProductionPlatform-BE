using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Chapter.Application.Commands.CreateChapter;
using MangaERP.Chapter.Application.Commands.ActivatePageTask;
using MangaERP.Chapter.Application.Commands.SubmitChapterForQA;
using MangaERP.Chapter.Application.Queries;

namespace MangaERP.Chapter.Presentation.Controllers;

[ApiController]
[Route("api/v1/chapters")]
[Authorize]
public class ChapterController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChapterController(IMediator mediator) => _mediator = mediator;

    /// <summary>MF2 Step 1: Mangaka creates a chapter under an active series.</summary>
    [HttpPost]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> CreateChapter([FromBody] CreateChapterCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetChapter), new { id = result.ChapterId }, result);
    }

    /// <summary>Get all chapters for a series.</summary>
    [HttpGet("series/{seriesId:guid}")]
    public async Task<IActionResult> GetChaptersBySeries(Guid seriesId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetChaptersBySeriesQuery(seriesId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Get chapter detail with page tasks.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetChapter(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetChapterDetailQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>MF2 Step 2: Activate a page task and assign an assistant.</summary>
    [HttpPost("{chapterId:guid}/pages/activate")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> ActivatePage(Guid chapterId, [FromBody] ActivatePageRequest request, CancellationToken cancellationToken)
    {
        var command = new ActivatePageTaskCommand(chapterId, request.PageNumber, request.AssignedAssistantId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>MF2 Step 10: Mangaka submits completed chapter for editorial QA.</summary>
    [HttpPost("{id:guid}/submit-for-qa")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> SubmitForQA(Guid id, [FromQuery] Guid mangakaId, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new SubmitChapterForQACommand(id, mangakaId), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

public record ActivatePageRequest(int PageNumber, Guid AssignedAssistantId);
