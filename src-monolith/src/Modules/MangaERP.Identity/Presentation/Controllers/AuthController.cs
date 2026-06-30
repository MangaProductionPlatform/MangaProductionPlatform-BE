using MediatR;
using MangaERP.Identity.Application.Commands.Login;
using MangaERP.Identity.Application.Commands.Logout;
using MangaERP.Identity.Application.Commands.RefreshToken;
using MangaERP.Identity.Application.Commands.ActivateAccount;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Identity.Presentation.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;

    public AuthController(IMediator mediator, IConfiguration config)
    {
        _mediator = mediator;
        _config   = config;
    }

    // ── Cookie helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the RefreshTokenExpiryDays from config (same source as JwtTokenService).
    /// </summary>
    private int RefreshExpiryDays
        => int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");

    /// <summary>
    /// Writes the refresh token as a secure httpOnly cookie.
    /// HttpOnly   = browser JS cannot read it (XSS protection).
    /// Secure     = HTTPS-only in production.
    /// SameSite   = Strict to mitigate CSRF.
    /// </summary>
    private void SetRefreshTokenCookie(string token)
    {
        Response.Cookies.Append("refreshToken", token, new CookieOptions
        {
            HttpOnly  = true,
            Secure    = true,   // set to false only in local HTTP dev if needed
            SameSite  = SameSiteMode.Strict,
            Expires   = DateTimeOffset.UtcNow.AddDays(RefreshExpiryDays),
            Path      = "/"
        });
    }

    /// <summary>Removes the refresh token cookie from the browser.</summary>
    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            Path     = "/"
        });
    }

    // ── Endpoints ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Authenticate and receive JWT access token in body + refresh token as httpOnly cookie.
    /// Response body: { accessToken, role, userId }
    /// Cookie: refreshToken (httpOnly, Secure, SameSite=Strict)
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(command, ct);

            // Set refresh token as httpOnly cookie — NOT in response body
            SetRefreshTokenCookie(result.NewRefreshToken);

            // Return only what the client needs in JS memory (no refreshToken)
            return Ok(new
            {
                result.AccessToken,
                result.Role,
                UserId = result.UserId.ToString()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Activate a provisioned account using the invitation token from email.
    /// </summary>
    [HttpPost("activate")]
    [AllowAnonymous]
    public async Task<IActionResult> Activate([FromBody] ActivateAccountCommand command, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (InvalidInvitationTokenException ex) { return BadRequest(new { message = ex.Message }); }
        catch (AccountAlreadyActivatedException ex) { return Conflict(new { message = ex.Message }); }
        catch (EntityNotFoundException ex)          { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Silently issue a new access token using the httpOnly refresh token cookie.
    /// The browser automatically sends the cookie — no body payload required.
    /// Response body: { accessToken }
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        // Read refresh token from the httpOnly cookie set at login
        var cookieToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrWhiteSpace(cookieToken))
            return Unauthorized(new { message = "No refresh token cookie found. Please log in." });

        try
        {
            var result = await _mediator.Send(new RefreshTokenCommand(cookieToken), ct);

            // Rotate: write the NEW refresh token cookie, replacing the old one
            SetRefreshTokenCookie(result.NewRefreshToken);

            // Return only the new access token in body
            return Ok(new { result.AccessToken });
        }
        catch (UnauthorizedAccessException ex)
        {
            // Ensure the stale cookie is cleaned up
            DeleteRefreshTokenCookie();
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Log out the current user by revoking the refresh token from the cookie.
    /// Uses [AllowAnonymous] so logout works even when the access token has expired.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var cookieToken = Request.Cookies["refreshToken"];

        if (!string.IsNullOrWhiteSpace(cookieToken))
        {
            // Revoke the specific refresh token from the cookie
            await _mediator.Send(new LogoutCommand(cookieToken), ct);
        }

        // Always delete the cookie, even if token was already expired/revoked
        DeleteRefreshTokenCookie();

        return Ok(new { message = "Logged out successfully." });
    }
}
