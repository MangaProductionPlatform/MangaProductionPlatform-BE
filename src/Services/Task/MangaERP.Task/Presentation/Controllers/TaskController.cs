using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Task.Application.Commands.SubmitArtworkLayer;
using MangaERP.Task.Application.Commands.ReviewLayer;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Enums;

namespace MangaERP.Task.Presentation.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaskController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// MF2 Step 4: Assistant uploads an artwork layer for an assigned page task.
    /// Automatically versions previous layers (MF7).
    /// </summary>
    [HttpPost("{pageTaskId:guid}/layers")]
    [Authorize(Roles = "Assistant")]
    public async Task<IActionResult> SubmitLayer(
        Guid pageTaskId,
        [FromBody] SubmitLayerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SubmitArtworkLayerCommand(
            pageTaskId,
            request.AssistantId,
            request.LayerType,
            request.FileUrlOriginal,
            request.FileUrlOptimized);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// MF2 Step 6: Mangaka accepts or rejects a submitted artwork layer.
    /// </summary>
    [HttpPost("{pageTaskId:guid}/review")]
    [Authorize(Roles = "Mangaka")]
    public async Task<IActionResult> ReviewLayer(
        Guid pageTaskId,
        [FromBody] ReviewLayerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new ReviewLayerCommand(
                pageTaskId,
                request.ReviewerMangakaId,
                request.IsAccepted,
                request.RejectionNote);

            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

public record SubmitLayerRequest(
    Guid AssistantId,
    LayerType LayerType,
    string FileUrlOriginal,
    string FileUrlOptimized);

public record ReviewLayerRequest(
    Guid ReviewerMangakaId,
    bool IsAccepted,
    string? RejectionNote);
