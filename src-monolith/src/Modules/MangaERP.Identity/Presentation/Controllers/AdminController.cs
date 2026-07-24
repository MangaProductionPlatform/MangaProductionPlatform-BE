using MediatR;
using MangaERP.Identity.Application.Commands.ProvisionAccount;
using MangaERP.Identity.Application.Commands.UpdateAccountRole;
using MangaERP.Identity.Application.Commands.UpdateAccountStatus;
using MangaERP.Identity.Application.Commands.UpdateAccount;
using MangaERP.Identity.Application.Commands.ResendActivation;
using MangaERP.Identity.Application.Commands.DeleteAccount;
using MangaERP.Identity.Application.Queries.ListUsers;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MangaERP.Identity.Presentation.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    public AdminController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// [Step 1-5] Provision a new corporate account for a team member.
    /// Generates username, creates account in PendingActivation state,
    /// and dispatches an invitation email with a secure activation link.
    /// </summary>
    /// <remarks>
    /// **Request body:**
    /// ```json
    /// { "fullName": "Nguyễn Văn Anh", "personalEmail": "anh@gmail.com", "role": 3 }
    /// ```
    /// **Role values:** EditorialBoard=1, TantouEditor=2, Mangaka=3, Assistant=4, EditorInChief=5.
    /// Admin=0 and Reader=99 are system-only and cannot be provisioned.
    /// 
    /// **Generated username example:** `anhnv.tt@company.com`
    /// </remarks>
    [HttpPost("accounts/provision")]
    [ProducesResponseType(typeof(ProvisionAccountResult), 201)]
    [ProducesResponseType(409)]  // personal email already active/pending
    [ProducesResponseType(400)]  // validation error
    [ProducesResponseType(401)]
    public async Task<IActionResult> ProvisionAccount(
        [FromBody] ProvisionAccountRequest request, CancellationToken ct)
    {
        try
        {
            var command = new ProvisionAccountCommand(
                request.FullName,
                request.PersonalEmail,
                request.Role,
                request.PhoneNumber,
                request.ManagingTantouId);
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(ProvisionAccount), new { result.UserId }, result);
        }
        catch (UserAlreadyExistsException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// List all provisioned accounts. Supports optional filtering by role and status.
    /// </summary>
    /// <remarks>
    /// **Example:** `GET /api/v1/admin/accounts?roleFilter=1&amp;statusFilter=0`
    /// 
    /// StatusFilter: PendingActivation=0, Active=1, Suspended=2, Deactivated=3
    /// </remarks>
    [HttpGet("accounts")]
    [ProducesResponseType(typeof(ListUsersResult), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ListAccounts(
        [FromQuery] UserRole? roleFilter,
        [FromQuery] AccountStatus? statusFilter,
        CancellationToken ct)
    {
        var query = new ListUsersQuery(roleFilter, statusFilter);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a single user account by ID.
    /// </summary>
    [HttpGet("accounts/{userId:guid}")]
    [ProducesResponseType(typeof(UserSummaryDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetAccount(Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new ListUsersQuery(), ct);
        var user = result.Users.FirstOrDefault(u => u.UserId == userId);
        if (user is null) return NotFound(new { message = $"User {userId} not found." });
        return Ok(user);
    }

    /// <summary>
    /// Update user's role.
    /// </summary>
    [HttpPatch("accounts/{userId:guid}/role")]
    [ProducesResponseType(typeof(UpdateAccountRoleResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateRole(
        Guid userId, [FromBody] UpdateRoleRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateAccountRoleCommand(userId, request.Role);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Update user's status.
    /// </summary>
    [HttpPatch("accounts/{userId:guid}/status")]
    [ProducesResponseType(typeof(UpdateAccountStatusResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateStatus(
        Guid userId, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateAccountStatusCommand(userId, request.Status);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Update user's details.
    /// </summary>
    [HttpPut("accounts/{userId:guid}")]
    [ProducesResponseType(typeof(UpdateAccountResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> UpdateAccount(
        Guid userId, [FromBody] UpdateAccountRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateAccountCommand(
                userId,
                request.FullName,
                request.PersonalEmail,
                request.Role,
                request.PhoneNumber,
                request.ManagingTantouId);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UserAlreadyExistsException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Resend account activation link.
    /// </summary>
    [HttpPost("accounts/{userId:guid}/resend-activation")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ResendActivation(Guid userId, CancellationToken ct)
    {
        try
        {
            var command = new ResendActivationCommand(userId);
            await _mediator.Send(command, ct);
            return Ok(new { message = "Activation link resent successfully." });
        }
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Delete user account.
    /// </summary>
    [HttpDelete("accounts/{userId:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> DeleteAccount(Guid userId, CancellationToken ct)
    {
        try
        {
            var command = new DeleteAccountCommand(userId);
            await _mediator.Send(command, ct);
            return Ok(new { message = "Account deleted successfully." });
        }
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Reassign Tantou Editor for a Mangaka account.
    /// </summary>
    [HttpPatch("mangaka/{mangakaId:guid}/tantou")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ChangeMangakaTantou(
        Guid mangakaId, [FromBody] ChangeTantouRequest request, CancellationToken ct)
    {
        try
        {
            var user = await _mediator.Send(new ListUsersQuery(), ct);
            var mangakaDto = user.Users.FirstOrDefault(u => u.UserId == mangakaId);
            if (mangakaDto is null) return NotFound(new { message = $"Mangaka {mangakaId} not found." });

            var command = new UpdateAccountCommand(
                mangakaId,
                mangakaDto.FullName ?? string.Empty,
                mangakaDto.PersonalEmail ?? $"{mangakaDto.Username}@company.com",
                UserRole.Mangaka,
                mangakaDto.PhoneNumber,
                request.NewTantouId);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (EntityNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

public record ProvisionAccountRequest(
    string FullName,
    string PersonalEmail,
    UserRole Role,
    string? PhoneNumber = null,
    Guid? ManagingTantouId = null
);

public record UpdateRoleRequest(UserRole Role);
public record UpdateStatusRequest(AccountStatus Status);
public record ChangeTantouRequest(Guid NewTantouId);
public record UpdateAccountRequest(
    string FullName,
    string PersonalEmail,
    UserRole Role,
    string? PhoneNumber = null,
    Guid? ManagingTantouId = null
);
