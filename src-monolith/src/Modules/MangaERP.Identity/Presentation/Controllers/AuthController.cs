using MediatR;
using MangaERP.Identity.Application.Commands.Login;
using MangaERP.Identity.Application.Commands.Logout;
using MangaERP.Identity.Application.Commands.RefreshToken;
using MangaERP.Identity.Application.Commands.ActivateAccount;
using MangaERP.Identity.Application.Commands.ForgotPassword;
using MangaERP.Identity.Application.Commands.ResetPassword;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Identity.Presentation.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;
    private readonly ITokenBlacklistService _blacklistService;

    public AuthController(
        IMediator mediator,
        IConfiguration config,
        ITokenBlacklistService blacklistService)
    {
        _mediator         = mediator;
        _config           = config;
        _blacklistService = blacklistService;
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
        // Cookie SameSite strategy:
        // - Development: SameSite=Lax + Secure=false
        //     localhost:5173 (FE) and localhost:8080 (BE) share the same registrable domain (localhost).
        //     Browsers treat them as same-site, so Lax works without requiring HTTPS.
        //     Note: SameSite=None REQUIRES Secure=true — cannot use None+Secure=false (browsers reject it).
        // - Production: SameSite=None + Secure=true
        //     FE (Vercel) and BE (Railway) are on different domains → cross-site.
        //     SameSite=None is mandatory. HTTPS is enforced by Railway, so Secure=true is safe.
        var isDev = _config["ASPNETCORE_ENVIRONMENT"] == "Development" ||
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        Response.Cookies.Append("refreshToken", token, new CookieOptions
        {
            HttpOnly  = true,
            Secure    = !isDev,                    // false on HTTP localhost, true on HTTPS production
            SameSite  = isDev
                            ? SameSiteMode.Lax     // same registrable domain (localhost) → Lax is enough
                            : SameSiteMode.None,   // cross-domain in prod → None required (HTTPS enforces Secure)
            Expires   = DateTimeOffset.UtcNow.AddDays(RefreshExpiryDays),
            Path      = "/"
        });
    }

    /// <summary>Removes the refresh token cookie from the browser.</summary>
    private void DeleteRefreshTokenCookie()
    {
        var isDev = _config["ASPNETCORE_ENVIRONMENT"] == "Development" ||
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure   = !isDev,
            SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
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
    [EnableRateLimiting("AuthPolicy")]
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
        // Extract Access Token JTI and Expiration to blacklist it immediately upon logout
        var jti = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value 
                  ?? User.FindFirst("jti")?.Value;

        var expClaim = User.FindFirst("exp")?.Value;
        if (!string.IsNullOrEmpty(jti) && !string.IsNullOrEmpty(expClaim) && long.TryParse(expClaim, out var expSeconds))
        {
            var expiryUtc = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            _blacklistService.Blacklist(jti, expiryUtc);
        }

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

    /// <summary>
    /// [AllowAnonymous] Yêu cầu gửi mã OTP khôi phục mật khẩu tới PersonalEmail đăng ký của người dùng.
    /// Quyết định gửi dựa trên Email đăng nhập (Username) được cung cấp.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(ForgotPasswordResult), 200)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ForgotPasswordCommand(request.Username), ct);
        return Ok(result);
    }

    /// <summary>
    /// [AllowAnonymous] Xác thực mã OTP và tiến hành thiết lập mật khẩu mới.
    /// Đồng thời hủy toàn bộ phiên đăng nhập/refresh token cũ để đảm bảo an toàn.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        try
        {
            var command = new ResetPasswordCommand(request.Username, request.Otp, request.NewPassword);
            await _mediator.Send(command, ct);
            return Ok(new { message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại." });
        }
        catch (ArgumentException ex)             { return BadRequest(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex)   { return BadRequest(new { message = ex.Message }); }
        catch (EntityNotFoundException ex)       { return BadRequest(new { message = ex.Message }); }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────
public record ForgotPasswordRequest(string Username);
public record ResetPasswordRequest(string Username, string Otp, string NewPassword);

