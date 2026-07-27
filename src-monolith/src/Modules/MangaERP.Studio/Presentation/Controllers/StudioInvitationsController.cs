using MediatR;
using MangaERP.Studio.Application.Commands.InviteAssistant;
using MangaERP.Studio.Application.Commands.RespondInvitation;
using MangaERP.Studio.Application.Commands.CancelInvitation;
using MangaERP.Studio.Application.Commands.RetryRegistrationDelivery;
using MangaERP.Studio.Application.Queries;
using MangaERP.Studio.Application.Queries.GetMyAssistants;
using MangaERP.Studio.Application.Commands.ManageCollaboration;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MangaERP.Studio.Presentation.Controllers;

[ApiController]
[Route("api/v1/studios")]
[Authorize]
public class StudioInvitationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public StudioInvitationsController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var guid) ? guid : Guid.Empty;
    }

    // ── MANGAKA APIs ──────────────────────────────────────────────────────────

    /// <summary>
    /// [Mangaka] Lấy danh sách Assistant thuộc phạm vi quản lý hợp lệ của Mangaka hiện tại.
    /// Route Canonical: GET /api/v1/mangakas/me/assistants
    /// Route Alias: GET /api/v1/studios/my-assistants
    /// </summary>
    [HttpGet("/api/v1/mangakas/me/assistants")]
    [HttpGet("my-assistants")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(MyAssistantsResponseDto), 200)]
    public async Task<IActionResult> GetMyAssistants(CancellationToken ct)
    {
        var query = new GetMyAssistantsQuery(GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Xem thông tin chi tiết của một Assistant thuộc phạm vi quản lý.
    /// Route Canonical: GET /api/v1/mangakas/me/assistants/{assistantId}
    /// Route Alias: GET /api/v1/studios/my-assistants/{assistantId}
    /// </summary>
    [HttpGet("/api/v1/mangakas/me/assistants/{assistantId:guid}")]
    [HttpGet("my-assistants/{assistantId:guid}")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(MangaERP.Studio.Application.Queries.GetAssistantDetail.AssistantDetailResponseDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAssistantDetail(Guid assistantId, CancellationToken ct)
    {
        try
        {
            var query = new MangaERP.Studio.Application.Queries.GetAssistantDetail.GetAssistantDetailQuery(GetUserId(), assistantId);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Mời Assistant vào studio của một Series.
    /// Backend tự phân nhánh:
    /// - TH1 (Email chưa có tài khoản): Tạo tài khoản PendingActivation + gửi email kích hoạt.
    ///   Sau khi kích hoạt và đăng nhập, Assistant nhận lời mời đang chờ để Accept/Decline.
    /// - TH2 (Email đã có tài khoản Assistant): Gửi push notification để Assistant Accept/Decline.
    /// </summary>
    [HttpPost("{seriesId:guid}/invitations")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(InviteAssistantResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> InviteAssistant(
        Guid seriesId,
        [FromBody] InviteAssistantRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new InviteAssistantCommand(
                MangakaId: GetUserId(),
                SeriesId: seriesId,
                AssistantEmail: request.AssistantEmail,
                Message: request.Message);

            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Xem danh sách tất cả lời mời đã gửi trong một Series (bất kể trạng thái).
    /// </summary>
    [HttpGet("{seriesId:guid}/invitations")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<StudioInvitationDto>), 200)]
    public async Task<IActionResult> GetSeriesInvitations(Guid seriesId, CancellationToken ct)
    {
        var query = new GetSeriesInvitationsQuery(GetUserId(), seriesId);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Xem danh sách Assistant đang hoạt động trong studio của một Series.
    /// Chỉ trả về các Assistant đã chấp nhận lời mời.
    /// </summary>
    [HttpGet("{seriesId:guid}/members")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<StudioMemberDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetStudioMembers(Guid seriesId, CancellationToken ct)
    {
        try
        {
            var query = new GetStudioMembersQuery(GetUserId(), seriesId);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Hủy lời mời vào studio.
    /// Side-effect: Status → Cancelled.
    /// </summary>
    [HttpPost("invitations/{invitationId:guid}/cancel")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CancelInvitation(Guid invitationId, CancellationToken ct)
    {
        try
        {
            var command = new CancelInvitationCommand(invitationId, GetUserId());
            await _mediator.Send(command, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("invitations/{invitationId:guid}/retry-registration")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(RetryRegistrationDeliveryResult), 200)]
    public async Task<IActionResult> RetryRegistrationDelivery(Guid invitationId, CancellationToken ct)
    {
        var result = await _mediator.Send(new RetryRegistrationDeliveryCommand(invitationId, GetUserId()), ct);
        return Ok(result);
    }

    // ── ASSISTANT APIs ────────────────────────────────────────────────────────

    /// <summary>
    /// [Assistant] Xem danh sách lời mời đang chờ xử lý (Status = Pending).
    /// </summary>
    [HttpGet("invitations/pending")]
    [Authorize(Roles = "Assistant")]
    [ProducesResponseType(typeof(IEnumerable<StudioInvitationDto>), 200)]
    public async Task<IActionResult> GetPendingInvitations(CancellationToken ct)
    {
        var query = new GetPendingInvitationsQuery(GetUserId());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Assistant] Chấp nhận lời mời vào studio.
    /// Side-effect: Status → Accepted, RespondedAt được ghi nhận.
    /// </summary>
    [HttpPost("invitations/{invitationId:guid}/accept")]
    [Authorize(Roles = "Assistant")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AcceptInvitation(Guid invitationId, CancellationToken ct)
    {
        try
        {
            var command = new AcceptInvitationCommand(invitationId, GetUserId());
            await _mediator.Send(command, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// [Assistant] Từ chối lời mời vào studio.
    /// Side-effect: Status → Declined, RespondedAt được ghi nhận.
    /// </summary>
    [HttpPost("invitations/{invitationId:guid}/decline")]
    [Authorize(Roles = "Assistant")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeclineInvitation(Guid invitationId, CancellationToken ct)
    {
        try
        {
            var command = new DeclineInvitationCommand(invitationId, GetUserId());
            await _mediator.Send(command, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("collaborations/{collaborationId:guid}/suspend")]
    [Authorize(Roles = "Mangaka,Admin")]
    public async Task<IActionResult> SuspendCollaboration(Guid collaborationId, [FromBody] CollaborationStateRequest request, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new SuspendCollaborationCommand(collaborationId, GetUserId(), User.IsInRole("Admin"), request.Mode, request.Reason, request.ExpectedConcurrencyToken), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("collaborations/{collaborationId:guid}/suspension-mode")]
    [Authorize(Roles = "Mangaka,Admin")]
    public async Task<IActionResult> ChangeSuspensionMode(Guid collaborationId, [FromBody] CollaborationStateRequest request, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new ChangeSuspensionModeCommand(collaborationId, GetUserId(), User.IsInRole("Admin"), request.Mode, request.Reason, request.ExpectedConcurrencyToken), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("collaborations/{collaborationId:guid}/reactivate")]
    [Authorize(Roles = "Mangaka,Admin")]
    public async Task<IActionResult> ReactivateCollaboration(Guid collaborationId, [FromBody] ReactivateCollaborationRequest request, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new ReactivateCollaborationCommand(collaborationId, GetUserId(), User.IsInRole("Admin"), request.ExpectedConcurrencyToken), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("collaborations/{collaborationId:guid}/request-ending")]
    [Authorize(Roles = "Mangaka,Admin")]
    public async Task<IActionResult> RequestEndingCollaboration(Guid collaborationId, [FromBody] RequestEndingCollaborationRequest request, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new RequestEndingCollaborationCommand(collaborationId, GetUserId(), User.IsInRole("Admin"), request.ExpectedConcurrencyToken), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("collaborations/{collaborationId:guid}/end")]
    [Authorize(Roles = "Mangaka,Admin")]
    public async Task<IActionResult> EndCollaboration(Guid collaborationId, [FromBody] EndCollaborationRequest request, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new EndCollaborationCommand(collaborationId, GetUserId(), User.IsInRole("Admin"), request.Reason, request.ExpectedConcurrencyToken), ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ConflictException ex) { return Conflict(new { message = ex.Message }); }
    }
}

// ── Request Models ────────────────────────────────────────────────────────────

public record InviteAssistantRequest(
    string AssistantEmail,
    string? Message
);

public record CollaborationStateRequest(
    CollaborationSuspensionMode Mode,
    string Reason,
    Guid ExpectedConcurrencyToken);

public record ReactivateCollaborationRequest(Guid ExpectedConcurrencyToken);
public record RequestEndingCollaborationRequest(Guid ExpectedConcurrencyToken);
public record EndCollaborationRequest(string Reason, Guid ExpectedConcurrencyToken);
