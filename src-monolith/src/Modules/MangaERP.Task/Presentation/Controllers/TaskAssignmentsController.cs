using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Task.Application.Commands.TaskAssignment;
using MangaERP.Task.Application.Commands.TaskProgress;
using MangaERP.Task.Application.Commands.TaskCompletion;
using MangaERP.Task.Application.Commands.CancelAssignment;
using MangaERP.Task.Application.Commands.ReassignTask;
using MangaERP.Task.Application.Queries.GetAssistantCandidates;
using MangaERP.Task.Application.Queries.TaskCheckpoints;
using System.Security.Claims;

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

    /// <summary>
    /// [Mangaka] Lấy danh sách Assistant candidate cho một task (kèm mã lý do không khả dụng và workload).
    /// Route Canonical: GET /api/v1/tasks/{taskId}/assistant-candidates
    /// </summary>
    [HttpGet("api/v1/tasks/{taskId:guid}/assistant-candidates")]
    [HttpGet("api/v1/page-tasks/{taskId:guid}/assistant-candidates")]
    [HttpGet("api/tasks/{taskId:guid}/assistant-candidates")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> GetAssistantCandidates(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var query = new GetAssistantCandidatesQuery(taskId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Lấy danh sách Assistant candidate cho Chapter trước khi tạo Task (kèm mã lý do không khả dụng và workload).
    /// Route Canonical: GET /api/v1/chapters/{chapterId}/assistant-candidates
    /// </summary>
    [HttpGet("api/v1/chapters/{chapterId:guid}/assistant-candidates")]
    [HttpGet("api/chapters/{chapterId:guid}/assistant-candidates")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> GetChapterAssistantCandidates(
        [FromRoute] Guid chapterId,
        CancellationToken ct)
    {
        var query = new GetChapterAssistantCandidatesQuery(chapterId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Gửi lời mời giao task cho Assistant (Single Assistant Model).
    /// Route Canonical: POST /api/v1/tasks/{taskId}/assignments
    /// </summary>
    [HttpPost("api/v1/tasks/{taskId:guid}/assignments")]
    [HttpPost("api/tasks/{taskId:guid}/assign")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> AssignTask(
        [FromRoute] Guid taskId,
        [FromBody] AssignTaskRequest request,
        CancellationToken ct)
    {
        Guid targetAssistantId = request.AssistantId != null && request.AssistantId != Guid.Empty
            ? request.AssistantId.Value
            : request.PrimaryAssistantId;

        if (targetAssistantId == Guid.Empty)
            return BadRequest(new { message = "Assistant ID is required." });

        var command = new AssignTaskToAssistantCommand(
            taskId,
            targetAssistantId,
            GetCurrentUserId(),
            request.Description,
            request.Deadline,
            request.DurationHours.HasValue ? TimeSpan.FromHours(request.DurationHours.Value) : null,
            request.ResponseDeadline);

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Deprecated] Task-level respond route has been retired under Direct Assignment model.
    /// Route Canonical: POST /api/v1/tasks/assignments/{attemptId}/respond
    /// </summary>
    [HttpPost("api/v1/tasks/assignments/{attemptId:guid}/respond")]
    [HttpPost("api/tasks/assignments/{attemptId:guid}/respond")]
    [Authorize(Roles = "Assistant")]
    public IActionResult RespondTaskAssignment(
        [FromRoute] Guid attemptId,
        [FromBody] RespondTaskAssignmentRequest request)
    {
        return StatusCode(StatusCodes.Status410Gone, new { message = "Task-level respond route has been retired. Task assignments take effect immediately upon assignment." });
    }

    /// <summary>
    /// [Mangaka] Hủy lời mời đang ở trạng thái PendingAcceptance.
    /// Route Canonical: POST /api/v1/tasks/assignments/{attemptId}/cancel
    /// </summary>
    [HttpPost("api/v1/tasks/assignments/{attemptId:guid}/cancel")]
    [HttpPost("api/task-assignments/{attemptId:guid}/cancel")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> CancelAssignment(
        [FromRoute] Guid attemptId,
        [FromBody] CancelAssignmentRequest? request,
        CancellationToken ct)
    {
        var command = new CancelAssignmentCommand(attemptId, GetCurrentUserId(), request?.Reason);
        var result = await _mediator.Send(command, ct);
        return Ok(new { success = result });
    }

    /// <summary>
    /// [Mangaka] Phân công lại (Reassign) task cho Assistant mới.
    /// Route Canonical: POST /api/v1/tasks/{taskId}/reassign
    /// </summary>
    [HttpPost("api/v1/tasks/{taskId:guid}/reassign")]
    [HttpPost("api/tasks/{taskId:guid}/reassign")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> ReassignTask(
        [FromRoute] Guid taskId,
        [FromBody] ReassignTaskRequest request,
        CancellationToken ct)
    {
        Guid targetAssistantId = request.NewAssistantId != null && request.NewAssistantId != Guid.Empty
            ? request.NewAssistantId.Value
            : (request.AssistantId != null && request.AssistantId != Guid.Empty ? request.AssistantId.Value : request.PrimaryAssistantId);

        if (targetAssistantId == Guid.Empty)
            return BadRequest(new { message = "New assistant ID is required for reassignment." });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Reassignment reason is required." });

        var command = new ReassignTaskCommand(
            taskId,
            targetAssistantId,
            GetCurrentUserId(),
            request.Reason,
            request.Deadline,
            request.ResponseDeadline,
            request.Description);

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Deprecated] Takeover workflow has been retired.
    /// </summary>
    [HttpPost("api/v1/tasks/{taskId:guid}/takeover")]
    [HttpPost("api/tasks/{taskId:guid}/request-takeover")]
    [Authorize(Roles = "Mangaka")]
    [Obsolete("Takeover workflow has been retired. Use POST /api/v1/tasks/{taskId}/reassign instead.")]
    public IActionResult RequestTakeover([FromRoute] Guid taskId)
    {
        return StatusCode(410, new { message = "Takeover workflow has been retired. Use POST /api/v1/tasks/{taskId}/reassign instead." });
    }

    /// <summary>
    /// Xem lịch sử phân công công việc của task (bao gồm currentAssignment và history).
    /// Route Canonical: GET /api/v1/tasks/{taskId}/assignment-history
    /// </summary>
    [HttpGet("api/v1/tasks/{taskId:guid}/assignment-history")]
    [HttpGet("api/tasks/{taskId:guid}/assignment-history")]
    [Authorize(Roles = "Mangaka,Assistant,TantouEditor")]
    public async Task<IActionResult> GetAssignmentHistory(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var query = new GetTaskAssignmentHistoryQuery(taskId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Xem tổng quan Workload của một Assistant.
    /// </summary>
    [HttpGet("api/v1/assistants/{assistantId:guid}/workload")]
    [HttpGet("api/assistants/{assistantId:guid}/workload")]
    [Authorize(Roles = "Mangaka,Assistant,TantouEditor")]
    public async Task<IActionResult> GetAssistantWorkload(
        [FromRoute] Guid assistantId,
        CancellationToken ct)
    {
        var query = new GetAssistantWorkloadQuery(assistantId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("api/v1/tasks/{taskId:guid}/progress")]
    [HttpPost("api/tasks/{taskId:guid}/progress")]
    [Authorize(Roles = "Assistant")]
    public async Task<IActionResult> SubmitProgress(
        [FromRoute] Guid taskId,
        [FromBody] SubmitProgressRequest request,
        CancellationToken ct)
    {
        var command = new SubmitTaskProgressCommand(taskId, request.ProgressPercent, request.Note, GetCurrentUserId());
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("api/v1/tasks/{taskId:guid}/progress")]
    [HttpGet("api/tasks/{taskId:guid}/progress")]
    [Authorize(Roles = "Mangaka,Assistant,TantouEditor")]
    public async Task<IActionResult> GetProgressHistory(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var query = new GetTaskProgressHistoryQuery(taskId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet("api/v1/tasks/{taskId:guid}/checkpoints")]
    [HttpGet("api/tasks/{taskId:guid}/checkpoints")]
    [Authorize(Roles = "Mangaka,Assistant,TantouEditor")]
    public async Task<IActionResult> GetCheckpoints(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var query = new GetTaskCheckpointsQuery(taskId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPost("api/v1/tasks/{taskId:guid}/complete")]
    [HttpPost("api/tasks/{taskId:guid}/complete")]
    [Authorize(Roles = "Assistant")]
    public async Task<IActionResult> CompleteTask(
        [FromRoute] Guid taskId,
        CancellationToken ct)
    {
        var command = new CompleteTaskCommand(taskId, GetCurrentUserId());
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}

public record AssignTaskRequest(
    Guid? AssistantId,
    Guid PrimaryAssistantId,
    Guid? BackupAssistantId,
    string? Description,
    DateTime? Deadline,
    double? DurationHours,
    DateTime? ResponseDeadline);

public record RespondTaskAssignmentRequest(bool Accept, string? RejectionReason, Guid? ExpectedConcurrencyToken);
public record SubmitProgressRequest(int ProgressPercent, string? Note);
public record RequestTakeoverRequest(string? Reason, double? WorkDurationHours);
public record CancelAssignmentRequest(string? Reason);
public record ReassignTaskRequest(
    Guid? NewAssistantId,
    Guid PrimaryAssistantId,
    Guid? BackupAssistantId,
    Guid? AssistantId,
    string Reason,
    DateTime? Deadline,
    DateTime? ResponseDeadline,
    string? Description);
