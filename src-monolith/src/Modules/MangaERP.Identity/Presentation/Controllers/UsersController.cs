using MediatR;
using MangaERP.Identity.Application.Commands.UpdateProfile;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Identity.Presentation.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    public UsersController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// Update or complete the authenticated user's profile information (PenName, DrawingSoftwares, BankAccount).
    /// </summary>
    [HttpPut("profile")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateProfileCommand(
                GetUserId(),
                request.PenName,
                request.DrawingSoftwares,
                request.BankAccountNumber
            );
            await _mediator.Send(command, ct);
            return Ok(new { message = "Profile updated successfully." });
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

public record UpdateProfileRequest(
    string? PenName,
    string[]? DrawingSoftwares,
    string? BankAccountNumber
);
