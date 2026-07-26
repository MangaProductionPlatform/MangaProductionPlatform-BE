using MediatR;
using MangaERP.Task.Application.Commands.ReviewLayer;
using MangaERP.Task.Application.Commands.BulkReviewLayers;
using MangaERP.Task.Application.Commands.SubmitArtworkLayer;
using MangaERP.Task.Application.Queries.GetAssignedTasks;
using MangaERP.Task.Application.Queries.GetChapterTasks;
using MangaERP.Task.Application.Queries.GetLayerHistory;
using MangaERP.Chapter.Application.Queries.GetTaskDetail;
using MangaERP.Chapter.Application.Commands.UpdateTaskDeadline;
using MangaERP.Chapter.Application.Commands.UpdateTaskDetails;
using MangaERP.Chapter.Application.Commands.CancelAndRecreateTask;
using MangaERP.Chapter.Application.Queries.GetBasePageVersions;
using MangaERP.Task.Application.Queries.GetLayerVersions;
using MangaERP.Task.Application.Commands.RollbackLayer;
using MangaERP.Task.Application.Queries.GetTaskComments;
using MangaERP.Task.Application.Commands.AddComment;
using MangaERP.Task.Application.Commands.CreateExtensionRequest;
using MangaERP.Task.Application.Commands.HandleExtensionRequest;
using MangaERP.Task.Application.Queries.GetExtensionRequests;
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

    [HttpGet("/api/v1/assistants/submissions")]
    [Authorize(Roles = "Assistant")]
    [ProducesResponseType(typeof(IEnumerable<MangaERP.Task.Application.Queries.GetAssistantSubmissions.AssistantSubmissionDto>), 200)]
    public async Task<IActionResult> GetAssistantSubmissions(CancellationToken ct)
    {
        var query = new MangaERP.Task.Application.Queries.GetAssistantSubmissions.GetAssistantSubmissionsQuery(GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "Mangaka,Assistant,TantouEditor")]
    [ProducesResponseType(typeof(IEnumerable<AssignedTaskDto>), 200)]
    public async Task<IActionResult> GetTasks([FromQuery] string? type, [FromQuery] string? status, CancellationToken ct)
    {
        var queryStatus = status;
        if (string.Equals(type, "Revision", StringComparison.OrdinalIgnoreCase))
        {
            queryStatus = "Incomplete";
        }
        
        var query = new GetAssignedTasksQuery(GetUserId(), queryStatus);
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
    [Authorize(Roles = "Mangaka,TantouEditor")]
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

    [HttpGet("{pageTaskId:guid}")]
    [Authorize(Roles = "Mangaka,Assistant,TantouEditor")]
    [ProducesResponseType(typeof(TaskDetailDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetTaskDetail(Guid pageTaskId, CancellationToken ct)
    {
        try
        {
            var query = new GetTaskDetailQuery(GetUserId(), pageTaskId);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpPatch("{pageTaskId:guid}/deadline")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(UpdateTaskDeadlineResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateTaskDeadline(
        Guid pageTaskId,
        [FromBody] UpdateTaskDeadlineRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new UpdateTaskDeadlineCommand(GetUserId(), pageTaskId, request.Deadline);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{pageTaskId:guid}/layers/{layerType}/versions")]
    [Authorize(Roles = "Mangaka,Assistant")]
    [ProducesResponseType(typeof(IEnumerable<LayerVersionDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetLayerVersions(Guid pageTaskId, string layerType, CancellationToken ct)
    {
        try
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var query = new GetLayerVersionsQuery(pageTaskId, layerType, GetUserId(), role);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpPost("{pageTaskId:guid}/layers/{layerType}/rollback")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(RollbackLayerResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RollbackLayer(
        Guid pageTaskId, string layerType, [FromBody] RollbackLayerRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RollbackLayerCommand(pageTaskId, layerType, request.Version, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}/comments")]
    [Authorize(Roles = "Mangaka,Assistant,TantouEditor")]
    [ProducesResponseType(typeof(IEnumerable<TaskCommentDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetComments(Guid id, CancellationToken ct)
    {
        try
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var query = new GetTaskCommentsQuery(id, GetUserId(), role);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize(Roles = "Mangaka,Assistant,TantouEditor")]
    [ProducesResponseType(typeof(TaskCommentDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentReq request, CancellationToken ct)
    {
        try
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var command = new AddCommentCommand(id, GetUserId(), role, request.Content);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{pageTaskId:guid}/extension-requests")]
    [Authorize(Roles = "Assistant")]
    [ProducesResponseType(typeof(CreateExtensionRequestResult), 200)]
    public async Task<IActionResult> CreateExtensionRequest(
        Guid pageTaskId,
        [FromBody] CreateExtensionReq request,
        CancellationToken ct)
    {
        try
        {
            var command = new CreateExtensionRequestCommand(
                GetUserId(),
                pageTaskId,
                request.Reason,
                request.RequestedDeadline);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("extension-requests/{requestId:guid}/handle")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(HandleExtensionRequestResult), 200)]
    public async Task<IActionResult> HandleExtensionRequest(
        Guid requestId,
        [FromBody] HandleExtensionReq request,
        CancellationToken ct)
    {
        try
        {
            var command = new HandleExtensionRequestCommand(
                GetUserId(),
                requestId,
                request.IsApproved,
                request.RejectionReason);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("extension-requests")]
    [Authorize(Roles = "Mangaka,Assistant")]
    [ProducesResponseType(typeof(IEnumerable<ExtensionRequestDto>), 200)]
    public async Task<IActionResult> GetExtensionRequests(
        [FromQuery] Guid? pageTaskId,
        CancellationToken ct)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var query = new GetExtensionRequestsQuery(GetUserId(), role, pageTaskId);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("{pageTaskId:guid}")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(UpdateTaskDetailsResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateTaskDetails(
        Guid pageTaskId,
        [FromBody] UpdateTaskDetailsRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new UpdateTaskDetailsCommand(
                GetUserId(),
                pageTaskId,
                request.Description,
                request.Deadline,
                request.TaskType,
                request.BaseImageUrl);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{pageTaskId:guid}/base-pages/versions")]
    [Authorize(Roles = "Mangaka,Assistant")]
    [ProducesResponseType(typeof(IEnumerable<BasePageVersionDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBasePageVersions(Guid pageTaskId, CancellationToken ct)
    {
        try
        {
            var query = new GetBasePageVersionsQuery(GetUserId(), pageTaskId);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpPost("{pageTaskId:guid}/cancel-and-recreate")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(CancelAndRecreateTaskResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CancelAndRecreateTask(
        Guid pageTaskId,
        [FromBody] CancelAndRecreateTaskRequest? request,
        CancellationToken ct)
    {
        try
        {
            var command = new CancelAndRecreateTaskCommand(
                GetUserId(),
                pageTaskId,
                request?.Reason,
                request?.ConfirmProgressLoss ?? false,
                request?.CopyTaskDetails ?? true);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

public record CancelAndRecreateTaskRequest(
    string? Reason,
    bool ConfirmProgressLoss = false,
    bool CopyTaskDetails = true
);

public record UpdateTaskDetailsRequest(
    string? Description,
    DateTime? Deadline,
    string? TaskType,
    string? BaseImageUrl
);

public record CreateExtensionReq(string Reason, DateTime RequestedDeadline);
public record HandleExtensionReq(bool IsApproved, string? RejectionReason);

public record AddCommentReq(string Content);

public record RollbackLayerRequest(int Version);

public record SubmitArtworkLayerRequest(
    string LayerType,
    string FileUrlOriginal,
    string? FileUrlOptimized);

public record ReviewLayerRequest(bool IsAccepted, string? RejectionNote);

public record BulkReviewItemRequest(Guid PageTaskId, bool IsAccepted, string? RejectionNote);

public record BulkReviewLayersRequest(List<BulkReviewItemRequest> Reviews);

public record UpdateTaskDeadlineRequest(DateTime? Deadline);
