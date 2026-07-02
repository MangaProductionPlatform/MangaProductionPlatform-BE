using MangaERP.Api.Queries.GetBoardReports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MangaERP.Api.Controllers;

/// <summary>
/// Cross-module Board endpoints — đặt ở Api layer vì cần data từ nhiều modules.
/// Route: /api/v1/board
/// </summary>
[ApiController]
[Route("api/v1/board")]
[Authorize(Roles = "EditorialBoard,EditorInChief")]
public class BoardController : ControllerBase
{
    private readonly IMediator _mediator;

    public BoardController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// [EditorialBoard, EditorInChief, Admin] Báo cáo tổng hợp cho Board.
    /// Bao gồm: submissions đang review, conflict escalated, và cancellation requests.
    /// </summary>
    /// <remarks>
    /// **Response:**
    /// ```json
    /// {
    ///   "submissions": {
    ///     "totalInReview": 4,
    ///     "pendingEB": 3,
    ///     "conflictEscalated": 1,
    ///     "approvedThisMonth": 5,
    ///     "rejectedThisMonth": 2
    ///   },
    ///   "cancellations": {
    ///     "pendingApproval": 2,
    ///     "approvedThisMonth": 1,
    ///     "rejectedThisMonth": 0
    ///   },
    ///   "generatedAt": "2026-07-01T..."
    /// }
    /// ```
    /// </remarks>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(BoardReportsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetReports(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBoardReportsQuery(), ct);
        return Ok(result);
    }
}
