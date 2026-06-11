using MediatR;
using MangaERP.Series.Application.Queries.GetMySeries;
using MangaERP.Series.Application.Queries.GetSeriesDetail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Series.Presentation.Controllers;

[ApiController]
[Route("api/v1/series")]
[Authorize]
public class SeriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeriesController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    private string GetUserRole()
        => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    // ── MANGAKA ───────────────────────────────────────────────────────────────

    /// <summary>
    /// [Mangaka] Lấy danh sách series của mình (đã được Editorial Board approve).
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<SeriesSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetMySeries(CancellationToken ct)
    {
        var query  = new GetMySeriesQuery(GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    // ── SHARED ────────────────────────────────────────────────────────────────

    /// <summary>
    /// [All authorized roles] Lấy chi tiết một series theo ID.
    /// Mangaka chỉ xem được series của mình. Staff xem được tất cả.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard,Admin")]
    [ProducesResponseType(typeof(SeriesDetailDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var query  = new GetSeriesDetailQuery(id, GetUserId(), GetUserRole());
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)        { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }
}
