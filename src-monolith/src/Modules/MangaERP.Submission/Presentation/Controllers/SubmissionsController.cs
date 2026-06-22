using MediatR;
using MangaERP.Submission.Application.Commands.CreateDraft;
using MangaERP.Submission.Application.Commands.SubmitProposal;
using MangaERP.Submission.Application.Commands.UpdateManuscript;
using MangaERP.Submission.Application.Commands.ReSubmitProposal;
using MangaERP.Submission.Application.Commands.UpdateDraftMetadata;
using MangaERP.Submission.Application.Commands.RejectSubmission;
using MangaERP.Submission.Application.Commands.RequestRevision;
using MangaERP.Submission.Application.Commands.ApproveSubmission;
using MangaERP.Submission.Application.Queries.GetMySubmissions;
using MangaERP.Submission.Application.Queries.GetSubmissionDetail;
using MangaERP.Submission.Application.Queries.GetSubmissionQueue;
using MangaERP.Submission.Application.Queries.GetFeedbackPins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MangaERP.Submission.Domain.Exceptions;
using MangaERP.Submission.Domain.Entities;
using FluentValidation;

namespace MangaERP.Submission.Presentation.Controllers;

[ApiController]
[Route("api/v1/submissions")]
public class SubmissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubmissionsController(IMediator mediator)
        => _mediator = mediator;

    private Guid GetUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
    }

    private string GetUserRole()
        => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

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

    // ── EDITORIAL BOARD VETTING FLOWS ────────────────────────────────────────

    /// <summary>
    /// [EditorialBoard / Admin] Get the active vetting queue.
    /// Editorial Board sees submissions with status Pending_EB_Review.
    /// </summary>
    [HttpGet("queue")]
    [Authorize(Roles = "EditorialBoard,Admin")]
    [ProducesResponseType(typeof(IEnumerable<SubmissionSummaryDto>), 200)]
    public async Task<IActionResult> GetQueue(CancellationToken ct)
    {
        try
        {
            var query = new GetSubmissionQueueQuery(GetUserRole());
            var result = await _mediator.Send(query, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { error = "Không có quyền", message = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { error = "Internal error", message = ex.Message }); }
    }

    /// <summary>
    /// [EditorialBoard] Request revision for a submission with visual feedback pins.
    /// Pending_EB_Review → Requires_Revision + feedback pins on canvas.
    /// </summary>
    [HttpPost("{id:guid}/request-revision")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(RequestRevisionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RequestRevision(Guid id, [FromBody] RevisionWithPinsRequest request, CancellationToken ct)
    {
        try
        {
            var pins = request.Pins?.Select(p => new FeedbackPinInput(
                p.PageIdentifier, p.CoordinateX, p.CoordinateY, p.Comment, p.Category
            )).ToList() ?? new List<FeedbackPinInput>();

            var command = new RequestRevisionCommand(id, GetUserId(), "EditorialBoard", request.Reason, pins);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(new { error = "Dữ liệu không hợp lệ", details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [EditorialBoard] Reject a submission permanently.
    /// Pending_EB_Review → EB_Rejected (permanently locked).
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(RejectSubmissionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] FeedbackRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RejectSubmissionCommand(id, GetUserId(), "EditorialBoard", request.Reason);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(new { error = "Dữ liệu không hợp lệ", details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [EditorialBoard] Approve a submission. Triggers series creation + TE assignment.
    /// Pending_EB_Review → EB_Approved + MangaSeries created + Mangaka assigned to TE.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(ApproveSubmissionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest request, CancellationToken ct)
    {
        try
        {
            var command = new ApproveSubmissionCommand(id, GetUserId(), request.AssignedEditorId);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (ValidationException ex) { return BadRequest(new { error = "Dữ liệu không hợp lệ", details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "Lỗi nghiệp vụ", message = ex.Message }); }
    }

    // ── SHARED FLOWS ──────────────────────────────────────────────────────────

    /// <summary>
    /// [All Authorized Roles] Get details of a single submission.
    /// Mangakas can only view their own submissions. Staff can view all.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard,Admin")]
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
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard,Admin")]
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
    [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard,Admin")]
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

public record ApproveRequest(Guid AssignedEditorId);

public record RevisionPinRequest(
    string PageIdentifier,
    double CoordinateX,
    double CoordinateY,
    string Comment,
    FeedbackPinCategory Category);

public record RevisionWithPinsRequest(
    string Reason,
    List<RevisionPinRequest>? Pins);
