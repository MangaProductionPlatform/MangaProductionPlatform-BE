using MediatR;
using MangaERP.Submission.Application.Commands.CreateDraft;
using MangaERP.Submission.Application.Commands.SubmitProposal;
using MangaERP.Submission.Application.Commands.UpdateManuscript;
using MangaERP.Submission.Application.Commands.ReSubmitProposal;
using MangaERP.Submission.Application.Commands.UpdateDraftMetadata;
using MangaERP.Submission.Application.Commands.StartReview;
using MangaERP.Submission.Application.Commands.RecommendToBoard;
using MangaERP.Submission.Application.Commands.RejectSubmission;
using MangaERP.Submission.Application.Commands.RequestRevision;
using MangaERP.Submission.Application.Commands.ApproveSubmission;
using MangaERP.Submission.Application.Queries.GetMySubmissions;
using MangaERP.Submission.Application.Queries.GetSubmissionDetail;
using MangaERP.Submission.Application.Queries.GetSubmissionQueue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MangaERP.Submission.Domain.Exceptions;

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

    /// <summary>
    /// [Mangaka] Update the manuscript URL of a draft or revision-required submission.
    /// </summary>
    [HttpPut("{id:guid}/manuscript")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(UpdateManuscriptResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateManuscript(Guid id, [FromBody] UpdateManuscriptRequest request, CancellationToken ct)
    {
        var command = new UpdateManuscriptCommand(id, GetUserId(), request.ManuscriptUrl);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Update metadata of a draft or revision-required submission.
    /// </summary>
    [HttpPut("{id:guid}/metadata")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(UpdateDraftMetadataResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] UpdateMetadataRequest request, CancellationToken ct)
    {
        var command = new UpdateDraftMetadataCommand(
            id, GetUserId(), request.Title, request.Description, request.Genre, request.CoverImageUrl);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// [Mangaka] Submit a draft proposal for the first time.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(SubmitProposalResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new SubmitProposalCommand(id, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Re-submit a proposal after addressing revision feedback.
    /// </summary>
    [HttpPost("{id:guid}/resubmit")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(ReSubmitProposalResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ReSubmit(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new ReSubmitProposalCommand(id, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [Mangaka] Get all own submissions.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Mangaka")]
    [ProducesResponseType(typeof(IEnumerable<SubmissionSummaryDto>), 200)]
    public async Task<IActionResult> GetMySubmissions([FromQuery] string? statusFilter, CancellationToken ct)
    {
        var query = new GetMySubmissionsQuery(GetUserId(), statusFilter);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    // ── VETTING / STAFF FLOWS ──────────────────────────────────────────────────

    /// <summary>
    /// [TantouEditor / EditorialBoard] Get the active vetting queues.
    /// TantouEditor sees Pending/UnderReview. EditorialBoard sees RecommendedToBoard.
    /// </summary>
    [HttpGet("queue")]
    [Authorize(Roles = "TantouEditor,EditorialBoard,Admin")]
    [ProducesResponseType(typeof(IEnumerable<SubmissionSummaryDto>), 200)]
    public async Task<IActionResult> GetQueue(CancellationToken ct)
    {
        var query = new GetSubmissionQueueQuery(GetUserRole());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [TantouEditor] Mark a pending submission as UnderReview by this editor.
    /// </summary>
    [HttpPost("{id:guid}/start-review")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(StartReviewResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> StartReview(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new StartReviewCommand(id, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Recommend an UnderReview submission to the Editorial Board.
    /// </summary>
    [HttpPost("{id:guid}/recommend")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(RecommendToBoardResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Recommend(Guid id, [FromBody] RecommendRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RecommendToBoardCommand(id, GetUserId(), request.RecommendationMessage);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Request revision for a submission.
    /// </summary>
    [HttpPost("{id:guid}/te-request-revision")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(RequestRevisionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> TeRequestRevision(Guid id, [FromBody] FeedbackRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RequestRevisionCommand(id, GetUserId(), "TantouEditor", request.Reason);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [EditorialBoard] Request revision for a submission.
    /// </summary>
    [HttpPost("{id:guid}/eb-request-revision")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(RequestRevisionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EbRequestRevision(Guid id, [FromBody] FeedbackRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RequestRevisionCommand(id, GetUserId(), "EditorialBoard", request.Reason);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [TantouEditor] Reject a submission.
    /// </summary>
    [HttpPost("{id:guid}/te-reject")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(typeof(RejectSubmissionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> TeReject(Guid id, [FromBody] FeedbackRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RejectSubmissionCommand(id, GetUserId(), "TantouEditor", request.Reason);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [EditorialBoard] Reject a submission.
    /// </summary>
    [HttpPost("{id:guid}/eb-reject")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(RejectSubmissionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> EbReject(Guid id, [FromBody] FeedbackRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RejectSubmissionCommand(id, GetUserId(), "EditorialBoard", request.Reason);
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
    }

    /// <summary>
    /// [EditorialBoard] Approve a recommended submission. Triggers series creation.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(typeof(ApproveSubmissionResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new ApproveSubmissionCommand(id, GetUserId());
            var result = await _mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidStateTransitionException ex) { return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message }); }
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
        var query = new GetSubmissionDetailQuery(id, GetUserId(), GetUserRole());
        var result = await _mediator.Send(query, ct);
        return Ok(result);
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

public record RecommendRequest(string RecommendationMessage);

public record FeedbackRequest(string Reason);
