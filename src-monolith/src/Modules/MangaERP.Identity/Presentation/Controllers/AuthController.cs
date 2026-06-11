using MediatR;
using MangaERP.Identity.Application.Commands.Login;
using MangaERP.Identity.Application.Commands.ActivateAccount;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace MangaERP.Identity.Presentation.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Authenticate and receive JWT access + refresh tokens.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { message = ex.Message }); }
    }

    /// <summary>Activate a provisioned account using the invitation token from email.</summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateAccountCommand command, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (InvalidInvitationTokenException ex) { return BadRequest(new { message = ex.Message }); }
        catch (AccountAlreadyActivatedException ex) { return Conflict(new { message = ex.Message }); }
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
