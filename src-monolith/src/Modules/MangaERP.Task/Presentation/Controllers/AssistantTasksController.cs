using MediatR;
using MangaERP.Task.Application.Queries.GetAssistantIncome;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Task.Presentation.Controllers;

[ApiController]
[Route("api/v1/assistant/tasks")]
[Authorize]
public class AssistantTasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssistantTasksController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    [HttpGet("income")]
    [Authorize(Roles = "Assistant")]
    [ProducesResponseType(typeof(AssistantIncomeDto), 200)]
    public async Task<IActionResult> GetIncome(CancellationToken ct)
    {
        var query = new GetAssistantIncomeQuery(GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
