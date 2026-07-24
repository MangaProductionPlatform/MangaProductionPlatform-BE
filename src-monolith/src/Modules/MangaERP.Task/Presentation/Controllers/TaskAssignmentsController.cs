using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Task.Application.Commands.TaskAssignment;
using MangaERP.Task.Application.Commands.TaskProgress;
using MangaERP.Task.Application.Commands.TaskCompletion;
using MangaERP.Task.Application.Queries.TaskCheckpoints;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MangaERP.Task.Presentation.Controllers;

[ApiController]
[Authorize]
public class TaskAssignmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaskAssignmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }

    [HttpPost("api/tasks/{taskId:guid}/assign")]
    public async Task<IActionResult> AssignTask(
        [FromRoute] Guid taskId,
        [FromBody] AssignTaskRequest request,
        CancellationToken ct)
    {
        var command = new AssignTaskToAssistantCommand(
            taskId,
            request.AssistantId,
            GetCurrentUserId(),
            request.Description,
            request.Deadline,
            request.DurationHours.HasValue ? TimeSpan.FromHours(request.DurationHours.Value) : null);

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("api/tasks/assignments/{attemptId:guid}/respond")]
    public async Task<IActionResult> RespondTaskAssignment(
        [FromRoute] Guid attemptId,
        [FromBody] RespondTaskAssignmentRequest request,
        CancellationToken ct)
    {
        var command = new RespondTaskAssignmentCommand(
            attemptId,
            request.Accept,
            request.RejectionReason,
            GetCurrentUserId(),
            request.ExpectedConcurrencyToken ?? Guid.Empty);

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("api/tasks/{taskId:guid}/assignment-history")]
    public async Task<IActionResult> GetAssignmentHistory(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var query = new GetTaskAssignmentHistoryQuery(taskId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("api/assistants/{assistantId:guid}/workload")]
    public async Task<IActionResult> GetAssistantWorkload(
        [FromRoute] Guid assistantId,
        CancellationToken ct)
    {
        var query = new GetAssistantWorkloadQuery(assistantId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("api/tasks/{taskId:guid}/progress")]
    public async Task<IActionResult> SubmitProgress(
        [FromRoute] Guid taskId,
        [FromBody] SubmitProgressRequest request,
        CancellationToken ct)
    {
        var command = new SubmitTaskProgressCommand(taskId, request.ProgressPercent, request.Note, GetCurrentUserId());
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("api/tasks/{taskId:guid}/progress")]
    public async Task<IActionResult> GetProgressHistory(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var query = new GetTaskProgressHistoryQuery(taskId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("api/tasks/{taskId:guid}/checkpoints")]
    public async Task<IActionResult> GetCheckpoints(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var query = new GetTaskCheckpointsQuery(taskId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("api/tasks/{taskId:guid}/complete")]
    public async Task<IActionResult> CompleteTask(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var command = new CompleteTaskCommand(taskId, GetCurrentUserId());
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}

public record AssignTaskRequest(Guid AssistantId, string? Description, DateTime? Deadline, double? DurationHours);
public record RespondTaskAssignmentRequest(bool Accept, string? RejectionReason, Guid? ExpectedConcurrencyToken);
public record SubmitProgressRequest(int ProgressPercent, string? Note);
