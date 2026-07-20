using MediatR;
using MangaERP.Studio.Application.Commands.InviteAssistant;
using MangaERP.Studio.Application.Commands.RespondInvitation;
using MangaERP.Studio.Application.Commands.CancelInvitation;
using MangaERP.Studio.Application.Commands.RetryRegistrationDelivery;
using MangaERP.Studio.Application.Queries;
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
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

// ── Request Models ────────────────────────────────────────────────────────────

public record InviteAssistantRequest(
    string AssistantEmail,
    string? Message
);
