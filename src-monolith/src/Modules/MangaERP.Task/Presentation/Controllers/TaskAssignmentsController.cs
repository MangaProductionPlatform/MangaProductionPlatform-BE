using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Task.Application.Commands.TaskAssignment;
using MangaERP.Task.Application.Commands.TaskProgress;
using MangaERP.Task.Application.Commands.TaskCompletion;
using MangaERP.Task.Application.Commands.CancelAssignment;
using MangaERP.Task.Application.Commands.ReassignTask;
using MangaERP.Task.Application.Queries.GetAssistantCandidates;
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

    /// <summary>
    /// [Mangaka] Lấy danh sách Assistant candidate cho một task (kèm mã lý do không khả dụng và workload).
    /// Route Canonical: GET /api/v1/tasks/{taskId}/assistant-candidates
    /// Tantou, Assistant và các role khác không có quyền gọi (Trả về 403 Forbidden).
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
    /// [Mangaka] Gửi lời mời giao task cho Primary Assistant và tùy chọn Backup Assistant.
    /// Route Canonical: POST /api/v1/tasks/{taskId}/assignments
    /// Tantou, Assistant và các role khác không có quyền gọi (Trả về 403 Forbidden).
    /// </summary>
    [HttpPost("api/v1/tasks/{taskId:guid}/assignments")]
    [HttpPost("api/tasks/{taskId:guid}/assign")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> AssignTask(
        [FromRoute] Guid taskId,
        [FromBody] AssignTaskRequest request,
        CancellationToken ct)
    {
        Guid primaryId = request.PrimaryAssistantId != Guid.Empty
            ? request.PrimaryAssistantId
            : (request.AssistantId ?? Guid.Empty);

        if (primaryId == Guid.Empty)
            return BadRequest(new { message = "Primary assistant ID is required." });

        var command = new AssignTaskToAssistantCommand(
            taskId,
            primaryId,
            request.BackupAssistantId,
            GetCurrentUserId(),
            request.Description,
            request.Deadline,
            request.DurationHours.HasValue ? TimeSpan.FromHours(request.DurationHours.Value) : null,
            request.ResponseDeadline);

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Assistant] Chấp nhận hoặc từ chối lời mời giao task.
    /// </summary>
    [HttpPost("api/v1/tasks/assignments/{attemptId:guid}/respond")]
    [HttpPost("api/tasks/assignments/{attemptId:guid}/respond")]
    [Authorize(Roles = "Assistant")]
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
    /// [Mangaka] Phân công lại (Reassign) task cho Primary/Backup mới.
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
        Guid primaryId = request.PrimaryAssistantId != Guid.Empty
            ? request.PrimaryAssistantId
            : (request.AssistantId ?? Guid.Empty);

        if (primaryId == Guid.Empty)
            return BadRequest(new { message = "Primary assistant ID is required for reassignment." });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Reassignment reason is required." });

        var command = new ReassignTaskCommand(
            taskId,
            primaryId,
            request.BackupAssistantId,
            GetCurrentUserId(),
            request.Reason,
            request.ResponseDeadline,
            request.Description);

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Kích hoạt Takeover cho Backup Assistant.
    /// Route Canonical: POST /api/v1/tasks/{taskId}/takeover
    /// Tantou, Assistant và các role khác không có quyền gọi (Trả về 403 Forbidden).
    /// </summary>
    [HttpPost("api/v1/tasks/{taskId:guid}/takeover")]
    [HttpPost("api/tasks/{taskId:guid}/request-takeover")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> RequestTakeover(
        [FromRoute] Guid taskId,
        [FromBody] RequestTakeoverRequest request,
        CancellationToken ct)
    {
        var command = new MangaERP.Task.Application.Commands.RequestTakeover.RequestTakeoverCommand(
            taskId,
            GetCurrentUserId(),
            request.Reason ?? "Assistant incident or timeout.",
            request.WorkDurationHours.HasValue ? TimeSpan.FromHours(request.WorkDurationHours.Value) : null);

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Xem lịch sử phân công công việc của task (bao gồm Current Primary, Current Backup và attempts).
    /// Read-only cho Mangaka, Assistant, và Tantou Editor quản lý series.
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
    Guid PrimaryAssistantId,
    Guid? BackupAssistantId,
    Guid? AssistantId,
    string? Description,
    DateTime? Deadline,
    double? DurationHours,
    DateTime? ResponseDeadline);

public record RespondTaskAssignmentRequest(bool Accept, string? RejectionReason, Guid? ExpectedConcurrencyToken);
public record SubmitProgressRequest(int ProgressPercent, string? Note);
public record RequestTakeoverRequest(string? Reason, double? WorkDurationHours);
public record CancelAssignmentRequest(string? Reason);
public record ReassignTaskRequest(
    Guid PrimaryAssistantId,
    Guid? BackupAssistantId,
    Guid? AssistantId,
    string Reason,
    DateTime? Deadline,
    DateTime? ResponseDeadline,
    string? Description);
