using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Ranking.Domain.Entities;

namespace MangaERP.Ranking.Presentation.Controllers;

[ApiController]
[Route("api/v1/rankings")]
public class RankingController : ControllerBase
{
    private readonly IMediator _mediator;

    public RankingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [Public] Get rankings for a specific period.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MangaERP.Ranking.Application.Queries.GetRankings.RankingListDto), 200)]
    public async Task<IActionResult> GetRankings([FromQuery] RankingPeriod period = RankingPeriod.Weekly, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var query = new MangaERP.Ranking.Application.Queries.GetRankings.GetRankingsQuery(period, limit);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Public] Get ranking for a specific series and period.
    /// </summary>
    [HttpGet("series/{seriesId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MangaERP.Ranking.Application.Queries.GetRankings.RankingItemDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSeriesRanking(Guid seriesId, [FromQuery] RankingPeriod period = RankingPeriod.Weekly, CancellationToken ct = default)
    {
        var query = new MangaERP.Ranking.Application.Queries.GetSeriesRanking.GetSeriesRankingQuery(seriesId, period);
        var result = await _mediator.Send(query, ct);
        
        if (result == null) return NotFound(new { message = $"Ranking not found for series {seriesId} in period {period}." });
        
        return Ok(result);
    }

    /// <summary>
    /// [Admin, EditorInChief] Force refresh rankings manually.
    /// </summary>
    [HttpPost("refresh")]
    [Authorize(Roles = "Admin,EditorInChief")]
    [ProducesResponseType(typeof(MangaERP.Ranking.Application.Commands.RefreshRankings.RefreshRankingsResult), 200)]
    public async Task<IActionResult> RefreshRankings(CancellationToken ct)
    {
        var command = new MangaERP.Ranking.Application.Commands.RefreshRankings.RefreshRankingsCommand();
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Admin, EditorInChief] Upload CSV file to import/update ranking data snapshot.
    /// </summary>
    [HttpPost("import-csv")]
    [Authorize(Roles = "Admin,EditorInChief")]
    [ProducesResponseType(typeof(MangaERP.Ranking.Application.Commands.ImportRankingCsv.ImportRankingCsvResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ImportCsv(
        Microsoft.AspNetCore.Http.IFormFile file,
        [FromQuery] RankingPeriod period = RankingPeriod.Weekly,
        [FromQuery] string? periodIdentifier = null,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please upload a non-empty CSV file." });

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only .csv files are supported." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var fileBytes = ms.ToArray();

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(userIdClaim, out var uploaderId);

        var command = new MangaERP.Ranking.Application.Commands.ImportRankingCsv.ImportRankingCsvCommand(
            uploaderId,
            file.FileName,
            fileBytes,
            period,
            periodIdentifier,
            dryRun);

        var result = await _mediator.Send(command, ct);
        if (!result.Success && !dryRun)
            return BadRequest(result);

        return Ok(result);
    }
}
