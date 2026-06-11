using MediatR;
using MangaERP.Identity.Application.Commands.ProvisionAccount;
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
    /// **Role values:** Admin=0, Mangaka=1, Assistant=2, TantouEditor=3, EditorialBoard=4, Reader=5
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
            var command = new ProvisionAccountCommand(request.FullName, request.PersonalEmail, request.Role);
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(ProvisionAccount), new { result.UserId }, result);
        }
        catch (UserAlreadyExistsException ex) { return Conflict(new { message = ex.Message }); }
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
}

public record ProvisionAccountRequest(
    string FullName,
    string PersonalEmail,
    UserRole Role
);
