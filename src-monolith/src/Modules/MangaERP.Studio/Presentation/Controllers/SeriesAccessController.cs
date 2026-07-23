using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Studio.Application.Commands.SeriesAccess;
using System.Security.Claims;

namespace MangaERP.Studio.Presentation.Controllers;

[ApiController]
[Route("api/studio/collaborations/{collaborationId:guid}/series-grants")]
[Authorize]
public class SeriesAccessController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeriesAccessController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }

    [HttpPost]
    public async Task<IActionResult> GrantSeriesAccess(
        [FromRoute] Guid collaborationId,
        [FromBody] GrantSeriesAccessRequest request,
        CancellationToken ct)
    {
        var command = new GrantSeriesAccessCommand(collaborationId, request.SeriesId, GetCurrentUserId());
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpDelete("{seriesId:guid}")]
    public async Task<IActionResult> RevokeSeriesAccess(
        [FromRoute] Guid collaborationId,
        [FromRoute] Guid seriesId,
        [FromBody] RevokeSeriesAccessRequest? request,
        CancellationToken ct)
    {
        var command = new RevokeSeriesAccessCommand(
            collaborationId,
            seriesId,
            GetCurrentUserId(),
            request?.Reason);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetCollaborationSeriesGrants(
        [FromRoute] Guid collaborationId,
        CancellationToken ct)
    {
        var query = new GetCollaborationSeriesGrantsQuery(collaborationId, GetCurrentUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}

public record GrantSeriesAccessRequest(Guid SeriesId);
public record RevokeSeriesAccessRequest(string? Reason);
