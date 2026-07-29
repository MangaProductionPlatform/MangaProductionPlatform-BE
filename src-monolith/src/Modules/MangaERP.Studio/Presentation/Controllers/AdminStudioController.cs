using MediatR;
using MangaERP.Studio.Application.Commands.AssignAssistantToMangaka;
using MangaERP.Studio.Application.Queries.GetAdminUnassignedAssistants;
using MangaERP.Studio.Application.Queries.GetMyAssistants;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Studio.Presentation.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminStudioController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminStudioController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// [Admin] Lấy danh sách Assistant tự do (chưa thuộc Studio nào) để phân công cho Mangaka.
    /// Route: GET /api/v1/admin/unassigned-assistants
    /// </summary>
    [HttpGet("unassigned-assistants")]
    [ProducesResponseType(typeof(AdminUnassignedAssistantsResponseDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetUnassignedAssistants(CancellationToken ct)
    {
        var query = new GetAdminUnassignedAssistantsQuery();
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Lấy danh sách Assistant đang thuộc quyền quản lý của một Mangaka cụ thể.
    /// Route: GET /api/v1/admin/mangakas/{mangakaId:guid}/assistants
    /// </summary>
    [HttpGet("mangakas/{mangakaId:guid}/assistants")]
    [ProducesResponseType(typeof(MyAssistantsResponseDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetMangakaAssistants(Guid mangakaId, CancellationToken ct)
    {
        var query = new GetMyAssistantsQuery(mangakaId);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Admin Canonical Route] Phân công một Assistant tự do cho một Mangaka.
    /// Route: POST /api/v1/admin/assistants/{assistantId}/assign-mangaka
    /// </summary>
    [HttpPost("assistants/{assistantId:guid}/assign-mangaka")]
    [ProducesResponseType(typeof(AssignAssistantToMangakaResult), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> AssignMangakaToAssistant(
        Guid assistantId,
        [FromBody] AdminAssignMangakaRequest request,
        CancellationToken ct)
    {
        if (assistantId == Guid.Empty)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = "AssistantId is required and cannot be empty." });
        }

        if (request is null || request.MangakaId == Guid.Empty)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = "MangakaId is required and cannot be empty." });
        }

        try
        {
            var command = new AssignAssistantToMangakaCommand(
                assistantId,
                request.MangakaId,
                GetUserId(),
                request.Reason);
            var result = await _mediator.Send(command, ct);
            return StatusCode(201, result);
        }
        catch (AdminAssignException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.ErrorCode, message = ex.Message });
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(new { code = "ENTITY_NOT_FOUND", message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { code = "CONFLICT", message = ex.Message });
        }
    }

    /// <summary>
    /// [Admin Alias Route - Deprecated] Legacy route phân công Assistant cho Mangaka.
    /// Route: POST /api/v1/admin/assign-assistant-to-mangaka
    /// </summary>
    [HttpPost("assign-assistant-to-mangaka")]
    [Obsolete("Use canonical route POST /api/v1/admin/assistants/{assistantId}/assign-mangaka instead.")]
    [ProducesResponseType(typeof(AssignAssistantToMangakaResult), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> LegacyAssignAssistantToMangaka(
        [FromBody] LegacyAssignAssistantToMangakaRequest request,
        CancellationToken ct)
    {
        if (request is null || request.AssistantId == Guid.Empty)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = "AssistantId is required." });
        }

        if (request.MangakaId == Guid.Empty)
        {
            return BadRequest(new { code = "VALIDATION_ERROR", message = "MangakaId is required." });
        }

        try
        {
            var command = new AssignAssistantToMangakaCommand(
                request.AssistantId,
                request.MangakaId,
                GetUserId(),
                request.Reason);
            var result = await _mediator.Send(command, ct);
            return StatusCode(201, result);
        }
        catch (AdminAssignException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.ErrorCode, message = ex.Message });
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(new { code = "ENTITY_NOT_FOUND", message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { code = "CONFLICT", message = ex.Message });
        }
    }
}

public record AdminAssignMangakaRequest(Guid MangakaId, string? Reason = null);

public record LegacyAssignAssistantToMangakaRequest(Guid AssistantId, Guid MangakaId, string? Reason = null);
