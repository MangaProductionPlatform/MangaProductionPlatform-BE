using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MangaERP.Submission.Application.Commands.CreateSubmission;
using MangaERP.Submission.Application.Commands.ApproveSubmission;
using MangaERP.Submission.Application.Commands.RejectSubmission;
using MangaERP.Submission.Application.Commands.RequestRevision;
using MangaERP.Submission.Application.Commands.RecommendSubmission;

namespace MangaERP.Submission.Presentation.Controllers;

[ApiController]
[Route("api/v1/submissions")]
[Authorize]
public class SubmissionController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubmissionController(IMediator mediator) => _mediator = mediator;

    /// <summary>MF1 Step 1-2: Creator submits a new series proposal.</summary>
    [HttpPost]
    [Authorize(Roles = "Reader,Mangaka")]
    public async Task<IActionResult> CreateSubmission([FromBody] CreateSubmissionCommand command, CancellationToken cancellationToken)
    {
        var submissionId = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(CreateSubmission), new { submissionId }, new { SubmissionId = submissionId });
    }

    /// <summary>MF1 Step 3: Tantou Editor recommends the submission to the Board.</summary>
    [HttpPost("{id:guid}/recommend")]
    [Authorize(Roles = "TantouEditor")]
    public async Task<IActionResult> Recommend(Guid id, [FromBody] RecommendRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new RecommendSubmissionCommand(id, request.ReviewerEditorId, request.FeedbackMessage), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>MF1 Step 4 → APPROVED: Editorial Board approves the submission.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "EditorialBoard")]
    public async Task<IActionResult> Approve(Guid id, [FromQuery] Guid reviewerId, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new ApproveSubmissionCommand(id, reviewerId), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>MF1 Step 4 → REJECTED: Editorial Board or Editor rejects the submission.</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "TantouEditor,EditorialBoard")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new RejectSubmissionCommand(id, request.ReviewerUserId, request.FeedbackMessage), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>MF1 Step 4 → REVISION_REQUIRED: Editorial Board or Editor requests changes.</summary>
    [HttpPost("{id:guid}/request-revision")]
    [Authorize(Roles = "TantouEditor,EditorialBoard")]
    public async Task<IActionResult> RequestRevision(Guid id, [FromBody] RevisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new RequestRevisionCommand(id, request.ReviewerUserId, request.FeedbackMessage), cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}

public record RecommendRequest(Guid ReviewerEditorId, string FeedbackMessage);
public record RejectRequest(Guid ReviewerUserId, string FeedbackMessage);
public record RevisionRequest(Guid ReviewerUserId, string FeedbackMessage);
