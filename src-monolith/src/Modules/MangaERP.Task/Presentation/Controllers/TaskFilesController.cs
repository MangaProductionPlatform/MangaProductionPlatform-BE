using MangaERP.Chapter.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Shared.Domain.Entities;
using MangaERP.Task.Application.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MangaERP.Task.Presentation.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TaskFilesController : ControllerBase
{
    private static readonly HashSet<string> AllowedFileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "raw", "inking", "coloring", "final", "layer", "script", "sketch"
    };

    private readonly IPageTaskRepository _taskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ICollaborationAuthorizationService _authService;
    private readonly IAuditEventRepository _auditRepo;

    public TaskFilesController(
        IPageTaskRepository taskRepo,
        IChapterRepository chapterRepo,
        ICollaborationAuthorizationService authService,
        IAuditEventRepository auditRepo)
    {
        _taskRepo = taskRepo;
        _chapterRepo = chapterRepo;
        _authService = authService;
        _auditRepo = auditRepo;
    }

    [HttpGet("{taskId}/files/{fileType}")]
    public async Task<IActionResult> StreamTaskFile(Guid taskId, string fileType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(fileType) || !AllowedFileTypes.Contains(fileType.Trim()))
        {
            return BadRequest(new { message = "Invalid fileType requested." });
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out Guid actorUserId))
            return Unauthorized();

        var task = await _taskRepo.GetByIdAsync(taskId, ct);
        if (task == null)
            return NotFound(new { message = "Task not found." });

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct);
        if (chapter == null)
            return NotFound(new { message = "Chapter not found." });

        // Authorization check: Must be task owner (Mangaka) or authorized assistant
        bool canAccess = await _authService.CanAccessTaskResourcesAsync(actorUserId, taskId, ct);
        if (!canAccess)
        {
            return StatusCode(403, new { message = "Forbidden. Task file access requires valid collaboration, series grant, and active assignment or ownership." });
        }

        // Audit private file access
        var safeFileType = fileType.Trim().ToLowerInvariant();
        var audit = new AuditEvent(
            "TaskFileAccessed",
            actorUserId,
            "PageTaskFile",
            task.Id,
            taskId: task.Id,
            metadataJson: $"{{\"fileType\":\"{safeFileType}\"}}");
        await _auditRepo.AddAsync(audit, ct);
        await _auditRepo.SaveChangesAsync(ct);

        // Stream safe memory stream (never return raw physical server paths)
        var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"[Task {taskId} File Content for {safeFileType}]"));
        return File(memoryStream, "image/png", $"task_{taskId}_{safeFileType}.png");
    }
}
