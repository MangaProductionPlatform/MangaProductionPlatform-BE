using System.Security.Claims;
using System.Data;
using MangaERP.Chapter.Domain.Entities;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Api.Controllers;

[ApiController]
[Route("api/v1/editorial-workflow")]
[Authorize]
public class EditorialWorkflowController : ControllerBase
{
    private readonly AppDbContext _db;
    public EditorialWorkflowController(AppDbContext db) => _db = db;

    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public record GuidanceRequest(string Guidance);
    public record DecisionRequest(EditorialDecision Decision, string? Feedback);

    [HttpGet("tantou/queue")]
    [Authorize(Roles = "TantouEditor")]
    public async Task<IActionResult> TantouQueue(CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.TantouEditor, RoleNames.TantouEditor, ct);
        return Ok(new
        {
            submissions = await _db.SeriesSubmissions
            .Where(x => x.AssignedEditorId == UserId && (x.Status == SubmissionStatus.Pending_Tantou_Review || x.Status == SubmissionStatus.Editorial_Rejected_To_Tantou))
            .Select(x => new { x.Id, x.Title, x.Status, x.CurrentRound, x.FeedbackMessage }).ToListAsync(ct),
            chapters = await _db.Chapters
            .Where(x => x.AssignedEditorId == UserId && (x.Status == ChapterStatus.ReadyForQA || x.Status == ChapterStatus.EditorialRejectedToTantou))
            .Select(x => new { x.Id, x.Title, x.Status, Round = x.EditorialRound, x.EditorialFeedback }).ToListAsync(ct)
        });
    }

    [HttpPost("tantou/{workType}/{workId:guid}/return")]
    [Authorize(Roles = "TantouEditor")]
    public async Task<IActionResult> ReturnFromTantou(string workType, Guid workId, GuidanceRequest request, CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.TantouEditor, RoleNames.TantouEditor, ct);
        if (ParseType(workType) == EditorialWorkType.SeriesSubmission)
        {
            var submission = await _db.SeriesSubmissions.FindAsync([workId], ct) ?? throw new KeyNotFoundException();
            submission.ReturnByTantou(UserId, request.Guidance);
            AddNotification(submission.SubmitterId, "Revision guidance from Tantou", request.Guidance, workId, "SeriesSubmission", $"/submissions/{workId}");
        }
        else
        {
            var chapter = await _db.Chapters.FindAsync([workId], ct) ?? throw new KeyNotFoundException();
            chapter.ReturnByTantou(UserId, request.Guidance);
            var authorId = await _db.MangaSeries.Where(x => x.Id == chapter.SeriesId).Select(x => x.AuthorId).SingleAsync(ct);
            AddNotification(authorId, "Chapter revision guidance from Tantou", request.Guidance, workId, "Chapter", $"/chapters/{workId}");
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("tantou/{workType}/{workId:guid}/recommend")]
    [Authorize(Roles = "TantouEditor")]
    public async Task<IActionResult> Recommend(string workType, Guid workId, CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.TantouEditor, RoleNames.TantouEditor, ct);
        var type = ParseType(workType);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;
            var lockedWork = await LockWorkAsync(type, workId, ct);
            int round;
            if (type == EditorialWorkType.SeriesSubmission)
            {
                var work = (SeriesSubmission)lockedWork;
                work.RecommendToEditorialBoard(UserId);
                round = work.CurrentRound;
            }
            else
            {
                var work = (ChapterEntity)lockedWork;
                work.RecommendToEditorialBoard(UserId);
                round = work.EditorialRound;
            }

            Guid authorId = type == EditorialWorkType.SeriesSubmission
                ? ((SeriesSubmission)lockedWork).SubmitterId
                : await _db.MangaSeries.Where(s => s.Id == ((ChapterEntity)lockedWork).SeriesId).Select(s => s.AuthorId).SingleAsync(ct);

            Guid? assignedTantouId = type == EditorialWorkType.SeriesSubmission
                ? ((SeriesSubmission)lockedWork).AssignedEditorId
                : ((ChapterEntity)lockedWork).AssignedEditorId;

            var existingAssignedReviewerIds = await _db.EditorialReviewAssignments
                .Where(x => x.WorkType == type && x.WorkId == workId && x.RoundNumber == round)
                .Select(x => x.ReviewerId)
                .ToListAsync(ct);

            var reviewers = await _db.Users
                .Where(x => x.AccountStatus == AccountStatus.Active && !x.IsDeleted)
                .Where(x => x.Role == UserRole.EditorialBoard || x.UserRoles.Any(ur => ur.Role.Name == RoleNames.EditorialBoard))
                .Where(x => x.Role != UserRole.EditorInChief && !x.UserRoles.Any(ur => ur.Role.Name == RoleNames.EditorInChief))
                .Where(x => x.Id != authorId)
                .Where(x => !assignedTantouId.HasValue || x.Id != assignedTantouId.Value)
                .Where(x => !existingAssignedReviewerIds.Contains(x.Id))
                .OrderBy(x => _db.EditorialReviewAssignments.Count(a => a.ReviewerId == x.Id && a.Status == EditorialReviewAssignmentStatus.Pending))
                .ThenBy(x => x.Id).Take(2).Select(x => x.Id).ToListAsync(ct);
            if (reviewers.Count != 2)
            {
                var eicIds = await _db.Users
                    .Where(u => u.AccountStatus == AccountStatus.Active && !u.IsDeleted)
                    .Where(u => u.Role == UserRole.EditorInChief || u.UserRoles.Any(ur => ur.Role.Name == RoleNames.EditorInChief))
                    .Select(u => u.Id).ToListAsync(ct);

                foreach (var eicId in eicIds)
                {
                    AddNotification(eicId, "ReviewerAssignmentRequired", "Insufficient eligible Editorial Board reviewers without conflict of interest. EIC intervention required.", workId, type.ToString(), "/editorial/conflicts");
                }
                await _db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);

                throw new ConflictException("ReviewerAssignmentRequired: Exactly two active, non-EIC Editorial Board reviewers without conflict of interest are required. Escalated to Editor-in-Chief.");
            }

            foreach (var reviewer in reviewers)
            {
                _db.EditorialReviewAssignments.Add(EditorialReviewAssignment.Assign(type, workId, round, reviewer));
                AddNotification(reviewer, "Editorial review assigned", "A confidential independent review is ready.", workId, type.ToString(), "/editorial/reviews");
            }
            await _db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Ok(new { workId, round, reviewerCount = 2 });
        });
    }

    [HttpGet("reviews")]
    [Authorize(Roles = "EditorialBoard")]
    public async Task<IActionResult> MyReviews(CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.EditorialBoard, RoleNames.EditorialBoard, ct);
        return Ok(await _db.EditorialReviewAssignments
            .Where(x => x.ReviewerId == UserId)
            .OrderByDescending(x => x.AssignedAt)
            .Select(x => new { x.Id, x.WorkType, x.WorkId, x.RoundNumber, x.Status, x.AssignedAt, x.ReviewedAt })
            .ToListAsync(ct));
    }

    [HttpGet("reviews/{assignmentId:guid}")]
    [Authorize(Roles = "EditorialBoard")]
    public async Task<IActionResult> ReviewDetail(Guid assignmentId, CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.EditorialBoard, RoleNames.EditorialBoard, ct);
        var mine = await _db.EditorialReviewAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId && x.ReviewerId == UserId, ct)
            ?? throw new KeyNotFoundException();
        var round = await _db.EditorialReviewAssignments
            .Where(x => x.WorkType == mine.WorkType && x.WorkId == mine.WorkId && x.RoundNumber == mine.RoundNumber).ToListAsync(ct);
        if (round.Count != 2) return Conflict(new { message = "Editorial rounds must contain exactly two reviewers." });
        var bothComplete = round.All(x => x.Status == EditorialReviewAssignmentStatus.Completed);
        object? completedReviews = bothComplete
            ? round.Select(x => new { x.ReviewerId, x.Decision, x.Feedback, x.ReviewedAt })
            : null;
        return Ok(new { mine.Id, mine.WorkType, mine.WorkId, mine.RoundNumber, mine.Status, mine.Decision, mine.Feedback, mine.AssignedAt, mine.ReviewedAt, bothComplete, completedReviews });
    }

    [HttpPost("reviews/{assignmentId:guid}/decision")]
    [Authorize(Roles = "EditorialBoard")]
    public async Task<IActionResult> Decide(Guid assignmentId, DecisionRequest request, CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.EditorialBoard, RoleNames.EditorialBoard, ct);
        var metadata = await _db.EditorialReviewAssignments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == assignmentId && x.ReviewerId == UserId, ct)
            ?? throw new KeyNotFoundException();
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
                : null;
            await LockWorkAsync(metadata.WorkType, metadata.WorkId, ct);
            var mine = await _db.EditorialReviewAssignments
                .SingleAsync(x => x.Id == assignmentId && x.ReviewerId == UserId, ct);
            mine.Complete(request.Decision, request.Feedback);

            var round = await _db.EditorialReviewAssignments
                .Where(x => x.WorkType == mine.WorkType && x.WorkId == mine.WorkId && x.RoundNumber == mine.RoundNumber).ToListAsync(ct);
            if (round.Count != 2) throw new ConflictException("Editorial rounds must contain exactly two reviewers.");
            if (round.Any(x => x.Status != EditorialReviewAssignmentStatus.Completed))
            {
                await _db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return (IActionResult)Ok(new { status = "Recorded", peerReviewConfidential = true });
            }

            var decisions = round.Select(x => x.Decision!.Value).ToArray();
            if (decisions.All(x => x == EditorialDecision.Approved))
                await ApproveWork(mine.WorkType, mine.WorkId, UserId, ct);
            else if (decisions.All(x => x == EditorialDecision.Rejected))
                await RejectWork(mine.WorkType, mine.WorkId, UserId, string.Join("\n\n", round.Select(x => x.Feedback)), ct);
            else
                await EscalateWork(mine.WorkType, mine.WorkId, ct);
            await _db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return (IActionResult)Ok(new { status = decisions.Distinct().Count() == 1 ? decisions[0].ToString() : "Escalated" });
        });
    }

    [HttpGet("conflicts")]
    [Authorize(Roles = "EditorInChief")]
    public async Task<IActionResult> Conflicts(CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.EditorInChief, RoleNames.EditorInChief, ct);
        return Ok(new
        {
            submissions = await _db.SeriesSubmissions.Where(x => x.Status == SubmissionStatus.Conflict_Escalated).Select(x => new { x.Id, x.Title, WorkType = "SeriesSubmission", x.CurrentRound }).ToListAsync(ct),
            chapters = await _db.Chapters.Where(x => x.Status == ChapterStatus.ConflictEscalated).Select(x => new { x.Id, x.Title, WorkType = "Chapter", Round = x.EditorialRound }).ToListAsync(ct)
        });
    }

    [HttpGet("conflicts/{workType}/{workId:guid}")]
    [Authorize(Roles = "EditorInChief")]
    public async Task<IActionResult> ConflictDetail(string workType, Guid workId, CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.EditorInChief, RoleNames.EditorInChief, ct);
        var type = ParseType(workType);
        var roundNumber = await CurrentRound(type, workId, ct);
        var reviews = await _db.EditorialReviewAssignments
            .Where(x => x.WorkType == type && x.WorkId == workId && x.RoundNumber == roundNumber)
            .ToListAsync(ct);
        if (reviews.Count != 2 || reviews.Any(x => x.Status != EditorialReviewAssignmentStatus.Completed)) return NotFound();
        if (reviews.Any(x => x.ReviewerId == UserId)) return Forbid();
        return Ok(new { workType = type, workId, roundNumber, reviews = reviews.Select(x => new { x.ReviewerId, x.Decision, x.Feedback, x.ReviewedAt }) });
    }

    [HttpPost("conflicts/{workType}/{workId:guid}/decision")]
    [Authorize(Roles = "EditorInChief")]
    public async Task<IActionResult> Resolve(string workType, Guid workId, DecisionRequest request, CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.EditorInChief, RoleNames.EditorInChief, ct);
        if (!Enum.IsDefined(request.Decision))
            throw new InvalidOperationException("Decision must be Approved or Rejected.");
        var type = ParseType(workType);
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
                : null;
            await LockWorkAsync(type, workId, ct);
            var round = await CurrentRound(type, workId, ct);
            var reviews = await _db.EditorialReviewAssignments
                .Where(x => x.WorkType == type && x.WorkId == workId && x.RoundNumber == round).ToListAsync(ct);
            if (reviews.Count != 2 || reviews.Any(x => x.Status != EditorialReviewAssignmentStatus.Completed) ||
                reviews.Select(x => x.Decision).Distinct().Count() != 2)
                throw new ConflictException("Only a completed split decision can be resolved by the Editor in Chief.");
            if (reviews.Any(x => x.ReviewerId == UserId))
                return (IActionResult)Forbid();
            if (request.Decision == EditorialDecision.Approved)
                await ApproveWork(type, workId, UserId, ct, true);
            else
                await RejectWork(type, workId, UserId, request.Feedback ?? string.Empty, ct);
            await _db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return (IActionResult)Ok(new { status = request.Decision.ToString() });
        });
    }

    [HttpGet("tantou/{workType}/{workId:guid}/feedback")]
    [Authorize(Roles = "TantouEditor")]
    public async Task<IActionResult> CompletedFeedback(string workType, Guid workId, CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.TantouEditor, RoleNames.TantouEditor, ct);
        var type = ParseType(workType);
        await EnsureTantou(type, workId, UserId, ct);
        var isFinalRejected = type == EditorialWorkType.SeriesSubmission
            ? (await _db.SeriesSubmissions.FindAsync([workId], ct))?.Status == SubmissionStatus.Editorial_Rejected_To_Tantou
            : (await _db.Chapters.FindAsync([workId], ct))?.Status == ChapterStatus.EditorialRejectedToTantou;
        if (!isFinalRejected) return Forbid();
        var round = await CurrentRound(type, workId, ct);
        var reviews = await _db.EditorialReviewAssignments.Where(x => x.WorkType == type && x.WorkId == workId && x.RoundNumber == round).ToListAsync(ct);
        if (reviews.Count != 2 || reviews.Any(x => x.Status != EditorialReviewAssignmentStatus.Completed)) return Forbid();
        return Ok(reviews.Select(x => new { x.Decision, x.Feedback, x.ReviewedAt }));
    }

    [HttpPost("tantou/{workType}/{workId:guid}/consolidate")]
    [Authorize(Roles = "TantouEditor")]
    public async Task<IActionResult> Consolidate(string workType, Guid workId, GuidanceRequest request, CancellationToken ct)
    {
        await EnsureCurrentUserRoleAsync(UserRole.TantouEditor, RoleNames.TantouEditor, ct);
        var type = ParseType(workType);
        if (type == EditorialWorkType.SeriesSubmission)
        {
            var submission = await _db.SeriesSubmissions.FindAsync([workId], ct) ?? throw new KeyNotFoundException();
            submission.ReturnConsolidatedGuidanceToMangaka(UserId, request.Guidance);
            AddNotification(submission.SubmitterId, "Revision guidance after editorial review", request.Guidance, workId, "SeriesSubmission", $"/submissions/{workId}");
        }
        else
        {
            var chapter = await _db.Chapters.FindAsync([workId], ct) ?? throw new KeyNotFoundException();
            chapter.ReturnConsolidatedGuidanceToMangaka(UserId, request.Guidance);
            var authorId = await _db.MangaSeries.Where(x => x.Id == chapter.SeriesId).Select(x => x.AuthorId).SingleAsync(ct);
            AddNotification(authorId, "Chapter revision guidance after editorial review", request.Guidance, workId, "Chapter", $"/chapters/{workId}");
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static EditorialWorkType ParseType(string value) => Enum.TryParse<EditorialWorkType>(value, true, out var type) ? type : throw new InvalidOperationException("Work type must be SeriesSubmission or Chapter.");
    private async Task<int> CurrentRound(EditorialWorkType type, Guid id, CancellationToken ct) => type == EditorialWorkType.SeriesSubmission
        ? (await _db.SeriesSubmissions.FindAsync([id], ct) ?? throw new KeyNotFoundException()).CurrentRound
        : (await _db.Chapters.FindAsync([id], ct) ?? throw new KeyNotFoundException()).EditorialRound;

    private async System.Threading.Tasks.Task EnsureTantou(EditorialWorkType type, Guid id, Guid userId, CancellationToken ct)
    {
        Guid? assigned = type == EditorialWorkType.SeriesSubmission
            ? (await _db.SeriesSubmissions.FindAsync([id], ct) ?? throw new KeyNotFoundException()).AssignedEditorId
            : (await _db.Chapters.FindAsync([id], ct) ?? throw new KeyNotFoundException()).AssignedEditorId;
        if (assigned != userId) throw new UnauthorizedAccessException("Only the assigned Tantou Editor can view completed feedback.");
    }

    private async System.Threading.Tasks.Task ApproveWork(EditorialWorkType type, Guid id, Guid actorId, CancellationToken ct, bool eic = false)
    {
        if (type == EditorialWorkType.Chapter)
        {
            var chapter = await _db.Chapters.FindAsync([id], ct) ?? throw new KeyNotFoundException();
            chapter.Approve();
            var authorId = await _db.MangaSeries.Where(x => x.Id == chapter.SeriesId).Select(x => x.AuthorId).SingleAsync(ct);
            AddNotification(authorId, "Chapter approved", chapter.Title, id, "Chapter", $"/chapters/{id}");
            return;
        }
        var submission = await _db.SeriesSubmissions.FindAsync([id], ct) ?? throw new KeyNotFoundException();
        if (eic) submission.ApproveByEIC(actorId); else submission.ApproveByBoard(actorId);
        if (!await _db.MangaSeries.AnyAsync(x => x.SubmissionId == id, ct))
            _db.MangaSeries.Add(MangaSeries.Create(submission.SubmitterId, submission.Id, submission.Title, submission.Description, submission.Genre, submission.CoverImageUrl));
        AddNotification(submission.SubmitterId, "Series submission approved", submission.Title, id, "SeriesSubmission", $"/submissions/{id}");
    }

    private async System.Threading.Tasks.Task RejectWork(EditorialWorkType type, Guid id, Guid actorId, string feedback, CancellationToken ct)
    {
        if (type == EditorialWorkType.SeriesSubmission)
        {
            var submission = await _db.SeriesSubmissions.FindAsync([id], ct) ?? throw new KeyNotFoundException();
            submission.RejectToTantou(actorId, feedback);
            AddNotification(submission.AssignedEditorId!.Value, "Editorial feedback ready for consolidation", "A rejected submission is waiting for Tantou guidance.", id, "SeriesSubmission", $"/editorial-workflow/tantou/SeriesSubmission/{id}/feedback");
        }
        else
        {
            var chapter = await _db.Chapters.FindAsync([id], ct) ?? throw new KeyNotFoundException();
            chapter.RejectToTantou(feedback);
            AddNotification(chapter.AssignedEditorId!.Value, "Editorial feedback ready for consolidation", "A rejected chapter is waiting for Tantou guidance.", id, "Chapter", $"/editorial-workflow/tantou/Chapter/{id}/feedback");
        }
    }

    private async System.Threading.Tasks.Task EscalateWork(EditorialWorkType type, Guid id, CancellationToken ct)
    {
        if (type == EditorialWorkType.SeriesSubmission)
            (await _db.SeriesSubmissions.FindAsync([id], ct) ?? throw new KeyNotFoundException()).EscalateConflict();
        else
            (await _db.Chapters.FindAsync([id], ct) ?? throw new KeyNotFoundException()).EscalateEditorialConflict();
        var eicIds = await _db.Users
            .Where(x => x.AccountStatus == AccountStatus.Active && !x.IsDeleted &&
                (x.Role == UserRole.EditorInChief || x.UserRoles.Any(ur => ur.Role.Name == RoleNames.EditorInChief)))
            .Select(x => x.Id).ToListAsync(ct);
        foreach (var eicId in eicIds)
            AddNotification(eicId, "Split editorial decision requires resolution", "Two completed reviews disagree.", id, type.ToString(), "/editorial-workflow/conflicts");
    }

    private async System.Threading.Tasks.Task<object> LockWorkAsync(EditorialWorkType type, Guid id, CancellationToken ct)
    {
        if (!_db.Database.IsRelational())
            return type == EditorialWorkType.SeriesSubmission
                ? await _db.SeriesSubmissions.FindAsync([id], ct) ?? throw new KeyNotFoundException()
                : await _db.Chapters.FindAsync([id], ct) ?? throw new KeyNotFoundException();

        if (type == EditorialWorkType.SeriesSubmission)
            return await _db.SeriesSubmissions
                .FromSqlInterpolated($@"SELECT * FROM ""SeriesSubmissions"" WHERE ""Id"" = {id} FOR UPDATE")
                .SingleAsync(ct);
        return await _db.Chapters
            .FromSqlInterpolated($@"SELECT * FROM ""Chapters"" WHERE ""Id"" = {id} FOR UPDATE")
            .SingleAsync(ct);
    }

    private async System.Threading.Tasks.Task EnsureCurrentUserRoleAsync(UserRole legacyRole, string rbacRole, CancellationToken ct)
    {
        var authorized = await _db.Users.AnyAsync(x => x.Id == UserId &&
            x.AccountStatus == AccountStatus.Active && !x.IsDeleted &&
            (x.Role == legacyRole || x.UserRoles.Any(ur => ur.Role.Name == rbacRole)), ct);
        if (!authorized) throw new UnauthorizedAccessException("The authenticated account does not hold the required active role.");
    }

    private void AddNotification(Guid receiverId, string title, string message, Guid workId, string entityType, string targetUrl) =>
        _db.Notifications.Add(new Notification { ReceiverId = receiverId, Title = title, Message = message, NotifyType = "EditorialWorkflow", RelatedEntityId = workId, RelatedEntityType = entityType, TargetUrl = targetUrl });
}
