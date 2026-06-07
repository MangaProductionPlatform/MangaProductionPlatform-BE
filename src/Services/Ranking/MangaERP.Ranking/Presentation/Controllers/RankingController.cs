using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Ranking.Application;

namespace MangaERP.Ranking.Presentation.Controllers;

[ApiController]
[Route("api/v1/ranking")]
[Authorize]
public class RankingController : ControllerBase
{
    private readonly IMediator _mediator;

    public RankingController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Editorial Board imports raw reader vote data for a period.
    /// Automatically re-aggregates and updates the ranking board.
    /// </summary>
    [HttpPost("votes")]
    [Authorize(Roles = "EditorialBoard")]
    public async Task<IActionResult> ImportVoteData([FromBody] ImportVoteDataCommand command, CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Get the ranking board for a specific vote period (e.g. '2025-W23').
    /// Accessible by all authenticated users.
    /// </summary>
    [HttpGet("board")]
    public async Task<IActionResult> GetRankingBoard([FromQuery] string votePeriod, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(votePeriod))
            return BadRequest(new { message = "votePeriod query parameter is required." });

        var result = await _mediator.Send(new GetRankingBoardQuery(votePeriod), cancellationToken);
        return Ok(result);
    }
}
