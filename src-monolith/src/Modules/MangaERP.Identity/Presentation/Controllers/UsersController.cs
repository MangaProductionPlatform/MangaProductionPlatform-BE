using MediatR;
using MangaERP.Identity.Application.Commands.UpdateProfile;
using MangaERP.Identity.Application.Commands.ChangePassword;
using MangaERP.Identity.Application.Commands.ChangeAvatar;
using MangaERP.Identity.Application.Queries.GetMe;
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

    // ── GET /users/me ─────────────────────────────────────────────────────────

    /// <summary>
    /// [All roles] Lấy thông tin profile đầy đủ của user đang đăng nhập.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(GetMeResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GetMeQuery(GetUserId()), ct);
            return Ok(result);
        }
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ── PUT /users/profile ────────────────────────────────────────────────────

    /// <summary>
    /// [All roles] Cập nhật thông tin profile (PenName, DrawingSoftwares, BankAccount).
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
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ── PUT /users/me/avatar ──────────────────────────────────────────────────

    /// <summary>
    /// [All roles] Cập nhật ảnh đại diện của user đang đăng nhập.
    /// </summary>
    [HttpPut("me/avatar")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ChangeAvatar([FromBody] ChangeAvatarRequest request, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new ChangeAvatarCommand(GetUserId(), request.AvatarUrl), ct);
            return Ok(new { message = "Avatar updated successfully." });
        }
        catch (EntityNotFoundException ex)       { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex)             { return BadRequest(new { message = ex.Message }); }
    }

    // ── PUT /users/me/change-password ────────────────────────────────────────

    /// <summary>
    /// [All roles] Tự đổi mật khẩu của user đang đăng nhập.
    /// </summary>
    [HttpPut("me/change-password")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new ChangePasswordCommand(GetUserId(), request.CurrentPassword, request.NewPassword), ct);
            return Ok(new { message = "Password changed successfully." });
        }
        catch (EntityNotFoundException ex)       { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex)   { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex)             { return BadRequest(new { message = ex.Message }); }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────
public record UpdateProfileRequest(
    string?  PenName,
    string[]? DrawingSoftwares,
    string?  BankAccountNumber
);

public record ChangeAvatarRequest(string AvatarUrl);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

