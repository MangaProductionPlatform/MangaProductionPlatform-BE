using MediatR;
using MangaERP.Task.Application.Commands.ReviewLayer;
using MangaERP.Task.Application.Commands.BulkReviewLayers;
using MangaERP.Task.Application.Commands.SubmitArtworkLayer;
using MangaERP.Task.Application.Queries.GetAssignedTasks;
using MangaERP.Task.Application.Queries.GetChapterTasks;
using MangaERP.Task.Application.Queries.GetLayerHistory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Task.Presentation.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    [HttpGet("assigned")]
    [Authorize(Roles = "Assistant")]
    [ProducesResponseType(typeof(IEnumerable<AssignedTaskDto>), 200)]
    public async Task<IActionResult> GetAssignedTasks([FromQuery] string? status, CancellationToken ct)
    {
        var query = new GetAssignedTasksQuery(GetUserId(), status);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("chapter/{chapterId:guid}")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<ChapterTaskDto>), 200)]
    public async Task<IActionResult> GetChapterTasks(Guid chapterId, CancellationToken ct)
    {
        try
        {
            var query = new GetChapterTasksQuery(GetUserId(), chapterId);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpPost("{pageTaskId:guid}/layers")]
    [Authorize(Roles = "Assistant")]
    [ProducesResponseType(typeof(SubmitArtworkLayerResult), 200)]
    public async Task<IActionResult> SubmitLayer(
        Guid pageTaskId,
        [FromBody] SubmitArtworkLayerRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new SubmitArtworkLayerCommand(
                GetUserId(),
                pageTaskId,
                request.LayerType,
                request.FileUrlOriginal,
                request.FileUrlOptimized);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{pageTaskId:guid}/review")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(ReviewLayerResult), 200)]
    public async Task<IActionResult> ReviewLayer(
        Guid pageTaskId,
        [FromBody] ReviewLayerRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new ReviewLayerCommand(
                GetUserId(),
                pageTaskId,
                request.IsAccepted,
                request.RejectionNote);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("bulk-review")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(BulkReviewLayersResult), 200)]
    public async Task<IActionResult> BulkReview(
        [FromBody] BulkReviewLayersRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new BulkReviewLayersCommand(
                GetUserId(),
                request.Reviews.Select(r => new BulkReviewItem(r.PageTaskId, r.IsAccepted, r.RejectionNote)).ToList());

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("layers/history")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<LayerHistoryDto>), 200)]
    public async Task<IActionResult> GetLayersHistory(
        [FromQuery] Guid? seriesId,
        [FromQuery] Guid? chapterId,
        [FromQuery] Guid? pageTaskId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        try
        {
            var query = new GetLayerHistoryQuery(
                GetUserId(), seriesId, chapterId, pageTaskId, status);
            
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }
}

public record SubmitArtworkLayerRequest(
    string LayerType,
    string FileUrlOriginal,
    string? FileUrlOptimized);

public record ReviewLayerRequest(bool IsAccepted, string? RejectionNote);

public record BulkReviewItemRequest(Guid PageTaskId, bool IsAccepted, string? RejectionNote);

public record BulkReviewLayersRequest(List<BulkReviewItemRequest> Reviews);
