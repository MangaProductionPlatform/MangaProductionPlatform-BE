using MediatR;
using MangaERP.Chapter.Application.Commands.ActivatePageTask;
using MangaERP.Chapter.Application.Commands.AddBasePage;
using MangaERP.Chapter.Application.Commands.CreateChapter;
using MangaERP.Chapter.Application.Commands.SubmitForQA;
using MangaERP.Chapter.Application.Queries.GetChapterDetail;
using MangaERP.Chapter.Application.Queries.GetChaptersBySeries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Chapter.Presentation.Controllers;

[ApiController]
[Route("api/v1/chapters")]
[Authorize]
public class ChaptersController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChaptersController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    [HttpPost]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(CreateChapterResult), 200)]
    public async Task<IActionResult> CreateChapter([FromBody] CreateChapterRequest request, CancellationToken ct)
    {
        try
        {
            var command = new CreateChapterCommand(
                GetUserId(),
                request.SeriesId,
                request.Title,
                request.ChapterNumber,
                request.TotalPages,
                request.AssignedEditorId);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("series/{seriesId:guid}")]
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard")]
    [ProducesResponseType(typeof(IEnumerable<ChapterListItemDto>), 200)]
    public async Task<IActionResult> GetChaptersBySeries(Guid seriesId, CancellationToken ct)
    {
        try
        {
            var query = new GetChaptersBySeriesQuery(GetUserId(), seriesId);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("{chapterId:guid}")]
    [Authorize(Roles = "Mangaka,TantouEditor")]
    [ProducesResponseType(typeof(ChapterDetailDto), 200)]
    public async Task<IActionResult> GetChapterDetail(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var query = new GetChapterDetailQuery(GetUserId(), chapterId);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("{chapterId:guid}/pages")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(AddBasePageResult), 200)]
    public async Task<IActionResult> AddBasePage(
        Guid chapterId,
        [FromBody] AddBasePageRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new AddBasePageCommand(GetUserId(), chapterId, request.PageNumber);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{chapterId:guid}/pages/activate")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(ActivatePageTaskResult), 200)]
    public async Task<IActionResult> ActivatePageTask(
        Guid chapterId,
        [FromBody] ActivatePageTaskRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new ActivatePageTaskCommand(
                GetUserId(),
                chapterId,
                request.PageNumber,
                request.AssignedAssistantId);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{chapterId:guid}/submit-for-qa")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(SubmitChapterForQAResult), 200)]
    public async Task<IActionResult> SubmitForQA(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var command = new SubmitChapterForQACommand(GetUserId(), chapterId);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

public record CreateChapterRequest(
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    int TotalPages,
    Guid? AssignedEditorId);

public record AddBasePageRequest(int PageNumber);

public record ActivatePageTaskRequest(int PageNumber, Guid AssignedAssistantId);
