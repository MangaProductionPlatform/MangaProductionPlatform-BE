using MangaERP.Segmentation.Application.Commands.CreateSegmentationTask;
using MangaERP.Segmentation.Application.Commands.UpdateSegmentationTaskStatus;
using MangaERP.Segmentation.Application.Queries.GetMySegmentationTasks;
using MangaERP.Segmentation.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Segmentation.Presentation.Controllers;

[ApiController]
[Route("api/segmentation/tasks")]
[Authorize]
public class SegmentationTasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public SegmentationTasksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateSegmentationTaskResult), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateTask(
        [FromBody] CreateSegmentationTaskRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new CreateSegmentationTaskCommand(
                request.PageId,
                request.MaskRle,
                request.Bbox,
                request.TaskType,
                request.Note,
                request.AssignedToUserId,
                request.AssignedToUserRole,
                CreatedByUserId: GetUserId());

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetMyTasks), null, result);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { message = "Validation failed.", errors = ex.Errors.Select(e => e.ErrorMessage) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(PagedSegmentationTaskResult), 200)]
    public async Task<IActionResult> GetMyTasks(
        [FromQuery] SegmentationTaskStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetMySegmentationTasksQuery(
            CurrentUserId: GetUserId(),
            StatusFilter: status,
            Page: page,
            PageSize: pageSize);

        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(UpdateSegmentationTaskStatusResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateSegmentationTaskStatusRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new UpdateSegmentationTaskStatusCommand(
                TaskId: id,
                CallerUserId: GetUserId(),
                NewStatus: request.NewStatus);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { message = "Validation failed.", errors = ex.Errors.Select(e => e.ErrorMessage) });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public record CreateSegmentationTaskRequest(
    Guid PageId,
    string MaskRle,
    int[] Bbox,
    SegmentationTaskType TaskType,
    string? Note,
    Guid AssignedToUserId,
    string? AssignedToUserRole);

public record UpdateSegmentationTaskStatusRequest(SegmentationTaskStatus NewStatus);
