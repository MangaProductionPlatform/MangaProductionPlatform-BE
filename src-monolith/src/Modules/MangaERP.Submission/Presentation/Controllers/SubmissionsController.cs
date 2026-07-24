using MediatR;
using MangaERP.Submission.Application.Commands.CreateDraft;
using MangaERP.Submission.Application.Commands.SubmitProposal;
using MangaERP.Submission.Application.Commands.UpdateManuscript;
using MangaERP.Submission.Application.Commands.ReSubmitProposal;
using MangaERP.Submission.Application.Commands.UpdateDraftMetadata;
using MangaERP.Submission.Application.Commands.RejectSubmission;
using MangaERP.Submission.Application.Commands.ApproveSubmission;
using MangaERP.Submission.Application.Commands.CastVote;
using MangaERP.Submission.Application.Commands.ResolveConflict;
using MangaERP.Submission.Application.Commands.DeleteDraft;
using MangaERP.Submission.Application.Queries.GetMySubmissions;
using MangaERP.Submission.Application.Queries.GetSubmissionDetail;
using MangaERP.Submission.Application.Queries.GetSubmissionQueue;
using MangaERP.Submission.Application.Queries.GetFeedbackPins;
using MangaERP.Submission.Application.Queries.GetSubmissionVotes;
using MangaERP.Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MangaERP.Submission.Domain.Exceptions;
using MangaERP.Submission.Domain.Entities;
using FluentValidation;
using MangaERP.Identity.Application.Ports;

namespace MangaERP.Submission.Presentation.Controllers;

[ApiController]
[Route("api/v1/submissions")]
public class SubmissionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserRepository _userRepo;

    public SubmissionsController(IMediator mediator, IUserRepository userRepo)
    {
        _mediator = mediator;
        _userRepo = userRepo;
    }

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    private string GetUserRole()
        => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    /// <summary>
    /// Converts the legacy UserRole enum string from JWT claim to the canonical RBAC RoleNames constant.
    /// This bridges the gap between single-role JWT and the RBAC system.
    /// </summary>
    private string GetRbacRoleName()
    {
        var jwtRole = GetUserRole();
        return jwtRole switch
        {
            "EditorialBoard" => RoleNames.EditorialBoard,
            "EditorInChief"  => RoleNames.EditorInChief,
            "Admin"          => RoleNames.Admin,
            "TantouEditor"   => RoleNames.TantouEditor,
            "Mangaka"        => RoleNames.Mangaka,
            _                => jwtRole
        };
    }

    // ── MANGAKA FLOWS ────────────────────────────────────────────────────────

    /// <summary>
    /// [Mangaka] Create a new draft submission proposal.
    /// </summary>
    [HttpPost("draft")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(CreateDraftResult), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateDraftRequest request, CancellationToken ct)
    {
        try
        {
            var command = new CreateDraftSubmissionCommand(
                SubmitterId: GetUserId(),
                Title: request.Title,
                Description: request.Description,
                Genre: request.Genre,
                CoverImageUrl: request.CoverImageUrl,
                ManuscriptUrl: request.ManuscriptUrl
            );

            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.SubmissionId }, result);
        }
        catch (ValidationException ex) { return BadRequest(new { error = "Dữ liệu không hợp lệ", details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Update the manuscript URL of a draft or revision-required submission.
    /// Chỉ được phép khi trạng thái là Draft hoặc Requires_Revision.
    /// </summary>
    [HttpPut("{id:guid}/manuscript")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(UpdateManuscriptResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateManuscript(Guid id, [FromBody] UpdateManuscriptRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateManuscriptCommand(id, GetUserId(), request.ManuscriptUrl);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(new { error = "Dữ liệu không hợp lệ", details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Update metadata of a draft or revision-required submission.
    /// Chỉ được phép khi trạng thái là Draft hoặc Requires_Revision.
    /// </summary>
    [HttpPut("{id:guid}/metadata")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(UpdateDraftMetadataResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] UpdateMetadataRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateDraftMetadataCommand(
                id, GetUserId(), request.Title, request.Description, request.Genre, request.CoverImageUrl);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(new { error = "Dữ liệu không hợp lệ", details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Submit a draft proposal for the first time.
    /// Draft → Pending_EB_Review. ManuscriptUrl phải đã được upload trước.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(SubmitProposalResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new SubmitProposalCommand(id, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(new { error = "Dữ liệu không hợp lệ", details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Re-submit a proposal after addressing revision feedback.
    /// Requires_Revision → Pending_EB_Review.
    /// </summary>
    [HttpPost("{id:guid}/resubmit")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(ReSubmitProposalResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ReSubmit(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new ReSubmitProposalCommand(id, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(new { error = "Dữ liệu không hợp lệ", details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Get all own submissions.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<SubmissionSummaryDto>), 200)]
    public async Task<IActionResult> GetMySubmissions([FromQuery] string? statusFilter, CancellationToken ct)
    {
        try
        {
            var query = new GetMySubmissionsQuery(GetUserId(), statusFilter);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (Exception ex) { return StatusCode(500, new { error = "Internal error", message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Xóa mềm (soft delete) một Draft submission của mình.
    /// Chỉ được phép khi status == Draft.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(DeleteDraftResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteDraft(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new DeleteDraftCommand(id, GetUserId());
            var result  = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)        { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex)   { return BadRequest(new { message = ex.Message }); }
    }

    // ── BOARD QUEUE ───────────────────────────────────────────────────────────

    /// <summary>
    /// [EditorialBoard / EditorInChief / Admin] Get proposal queue.
    /// - EditorialBoard: all Pending_EB_Review submissions (everyone can see, first-come-first-serve for 2 slots).
    /// - EditorInChief: Conflict_Escalated (priority) + Pending_EB_Review.
    /// - Admin: all Pending_EB_Review.
    /// Returns proposals even after 2/2 votes so the list stays visible (vote button will be blocked server-side).
    /// </summary>
    [HttpGet("queue")]
    [Authorize(Roles = "EditorialBoard,EditorInChief,Admin")]
    [ProducesResponseType(typeof(IEnumerable<SubmissionSummaryDto>), 200)]
    public async Task<IActionResult> GetQueue(CancellationToken ct)
    {
        try
        {
            var query = new GetSubmissionQueueQuery(GetRbacRoleName(), GetUserId());
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = "Internal error", message = ex.Message }); }
    }

    // ── SHARED FLOWS ──────────────────────────────────────────────────────────

    /// <summary>
    /// [All Authorized Roles] Get details of a single submission.
    /// Mangakas can only view their own submissions. Staff can view all.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Mangaka,EditorialBoard,EditorInChief,Admin")]
    [ProducesResponseType(typeof(SubmissionDetailDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var query = new GetSubmissionDetailQuery(id, GetUserId(), GetUserRole());
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
    }

    // ── FEEDBACK PINS QUERY ───────────────────────────────────────────────────

    /// <summary>
    /// [All Authorized Roles] Get active feedback pins for a submission's canvas.
    /// Mangakas can only view pins on their own submissions.
    /// </summary>
    [HttpGet("{id:guid}/feedback-pins")]
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard,EditorInChief,Admin")]
    [ProducesResponseType(typeof(IEnumerable<FeedbackPinDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetFeedbackPins(Guid id, CancellationToken ct)
    {
        try
        {
            var query = new GetFeedbackPinsQuery(id, GetUserId(), GetUserRole(), false);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
    }

    /// <summary>
    /// [All Authorized Roles] Get all feedback pins including archived history.
    /// </summary>
    [HttpGet("{id:guid}/feedback-pins/history")]
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard,EditorInChief,Admin")]
    [ProducesResponseType(typeof(IEnumerable<FeedbackPinDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetFeedbackPinsHistory(Guid id, CancellationToken ct)
    {
        try
        {
            var query = new GetFeedbackPinsQuery(id, GetUserId(), GetUserRole(), true);
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
    }
}

// ── REQUEST MODELS ────────────────────────────────────────────────────────────

public record CreateDraftRequest(
    string Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl,
    string? ManuscriptUrl);

public record UpdateManuscriptRequest(string ManuscriptUrl);

public record UpdateMetadataRequest(
    string Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl);

public record FeedbackRequest(string Reason);

public record RevisionPinRequest(
    string PageIdentifier,
    double CoordinateX,
    double CoordinateY,
    string Comment,
    FeedbackPinCategory Category);

public record RevisionWithPinsRequest(
    string Reason,
    List<RevisionPinRequest>? Pins);

/// <summary>Request body for POST /{id}/vote</summary>
public record CastVoteRequest(
    string VoteType,
    string? Comment,
    List<RevisionPinRequest>? FeedbackPins);

/// <summary>Request body for POST /{id}/resolve-conflict</summary>
public record ResolveConflictRequest(
    string FinalDecision,
    string FeedbackMessage);
