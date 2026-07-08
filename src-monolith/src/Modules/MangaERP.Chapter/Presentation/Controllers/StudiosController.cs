using MediatR;
using MangaERP.Studio.Application.Commands.RemoveStudioMember;
using MangaERP.Chapter.Application.Queries.GetStudioTasksBoard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Chapter.Presentation.Controllers;

[ApiController]
[Route("api/v1/studios")]
[Authorize]
public class StudiosController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudiosController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    [HttpDelete("{seriesId:guid}/members/{assistantId:guid}")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveMember(Guid seriesId, Guid assistantId, CancellationToken ct)
    {
        try
        {
            var command = new RemoveStudioMemberCommand(GetUserId(), seriesId, assistantId);
            await _mediator.Send(command, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{seriesId:guid}/tasks/board")]
    [Authorize(Roles = "Mangaka,TantouEditor,Assistant")]
    [ProducesResponseType(typeof(StudioTasksBoardDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetTasksBoard(Guid seriesId, CancellationToken ct)
    {
        try
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var query = new GetStudioTasksBoardQuery(seriesId, GetUserId(), role);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }
}
