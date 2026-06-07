using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Series.Application.Queries;

namespace MangaERP.Series.Presentation.Controllers;

[ApiController]
[Route("api/v1/series")]
[Authorize]
public class SeriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeriesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get all active series (publicly browsable).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllActive(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllActiveSeriesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Get series detail by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSeries(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSeriesDetailQuery(id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Get all series by Mangaka (own series list).</summary>
    [HttpGet("by-author/{authorId:guid}")]
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard")]
    public async Task<IActionResult> GetByAuthor(Guid authorId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSeriesByAuthorQuery(authorId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Editorial Board cancels a series.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "EditorialBoard")]
    public async Task<IActionResult> CancelSeries(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new CancelSeriesCommand(id), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
