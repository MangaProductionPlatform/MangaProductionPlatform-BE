using FluentValidation;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MangaERP.Submission.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace MangaERP.Submission.Application.Commands.ResolveConflict;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Editor-in-Chief phân xử tranh chấp: Conflict_Escalated → final decision.
/// </summary>
public record ResolveConflictCommand(
    Guid SubmissionId,
    Guid EicId,               // extracted from JWT by controller
    VoteType FinalDecision,   // APPROVE | REJECT | REQ_REVISION
    string FeedbackMessage
) : IRequest<ResolveConflictResult>;

public record ResolveConflictResult(
    Guid SubmissionId,
    string NewStatus,
    string FinalDecision,
    string FeedbackMessage,
    DateTime ResolvedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class ResolveConflictHandler : IRequestHandler<ResolveConflictCommand, ResolveConflictResult>
{
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IDbContextProvider _dbContextProvider;
    private readonly INotificationService _notificationService;

    public ResolveConflictHandler(
        ISubmissionRepository submissionRepo,
        IUserRepository userRepo,
        ISeriesRepository seriesRepo,
        IDbContextProvider dbContextProvider,
        INotificationService notificationService)
    {
        _submissionRepo = submissionRepo;
        _userRepo = userRepo;
        _seriesRepo = seriesRepo;
        _dbContextProvider = dbContextProvider;
        _notificationService = notificationService;
    }

    public async Task<ResolveConflictResult> Handle(ResolveConflictCommand cmd, CancellationToken ct)
    {
        var db = (DbContext)_dbContextProvider.GetDbContext();
        var strategy = db.Database.CreateExecutionStrategy();

        ResolveConflictResult? result = null;
        string? postCommitAction = null;
        SeriesSubmission? loadedSubmission = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // ── 1. Validate EIC role via RBAC ──────────────────────────────────
            var hasRole = await _userRepo.HasRbacRoleAsync(cmd.EicId, RoleNames.EditorInChief, ct);
            if (!hasRole)
                throw new UnauthorizedAccessException("Chỉ Editor-in-Chief mới có thể phân xử tranh chấp.");

            // ── 2. Load submission ────────────────────────────────────────────
            var submission = await _submissionRepo.GetByIdAsync(cmd.SubmissionId, ct)
                ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

            // ── 3. Validate status ────────────────────────────────────────────
            if (submission.Status != SubmissionStatus.Conflict_Escalated)
                throw new InvalidStateTransitionException(
                    $"Chỉ có thể phân xử khi bản thảo ở trạng thái Conflict_Escalated. Trạng thái hiện tại: {submission.Status}");

            // ── 4. Apply decision ─────────────────────────────────────────────
            switch (cmd.FinalDecision)
            {
                case VoteType.APPROVE:
                    submission.ApproveByEIC(cmd.EicId);
                    postCommitAction = "APPROVE";
                    break;

                case VoteType.REJECT:
                    submission.RejectByEIC(cmd.EicId, cmd.FeedbackMessage);
                    postCommitAction = "REJECT";
                    break;

                case VoteType.REQ_REVISION:
                    // Increments CurrentRound — unlocks new voting round for EB
                    submission.RequestRevisionByEIC(cmd.EicId, cmd.FeedbackMessage);
                    postCommitAction = "REVISION";
                    break;

                default:
                    throw new ArgumentException($"Quyết định không hợp lệ: {cmd.FinalDecision}");
            }

            await _submissionRepo.SaveChangesAsync(ct);
            loadedSubmission = submission;

            result = new ResolveConflictResult(
                submission.Id,
                submission.Status.ToString(),
                cmd.FinalDecision.ToString(),
                cmd.FeedbackMessage,
                submission.ReviewedAt!.Value);

            await tx.CommitAsync(ct);
        });

        // ── 5. Post-commit side effects ───────────────────────────────────────
        if (postCommitAction == "APPROVE" && loadedSubmission != null)
        {
            await HandleApprovePostCommitAsync(loadedSubmission, ct);
        }
        else if (postCommitAction == "REJECT" && loadedSubmission != null)
        {
            await _notificationService.NotifySubmissionRejectedAsync(
                receiverId:      loadedSubmission.SubmitterId,
                submissionId:    loadedSubmission.Id,
                feedbackMessage: cmd.FeedbackMessage,
                ct:              ct);
        }
        else if (postCommitAction == "REVISION" && loadedSubmission != null)
        {
            await _notificationService.NotifySubmissionRevisionAsync(
                receiverId:  loadedSubmission.SubmitterId,
                submissionId: loadedSubmission.Id,
                message:     $"[Tổng Biên Tập] {cmd.FeedbackMessage}",
                pinCount:    0,
                targetUrl:   "/mangaka/submissions",
                ct:          ct);
        }

        return result!;
    }

    private async Task HandleApprovePostCommitAsync(SeriesSubmission submission, CancellationToken ct)
    {
        var db = (DbContext)_dbContextProvider.GetDbContext();
        var strategy = db.Database.CreateExecutionStrategy();

        Guid seriesId = Guid.Empty;
        Guid selectedTeId = Guid.Empty;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var allTE = await _userRepo.GetByRoleAsync(UserRole.TantouEditor, ct);
            var activeTE = allTE.Where(u => u.AccountStatus == AccountStatus.Active).ToList();
            if (!activeTE.Any())
                throw new InvalidOperationException("Không có Tantou Editor nào đang hoạt động để gán.");

            var activeTeIds = activeTE.Select(te => te.Id).ToList();
            var loads = await _userRepo.GetTantouEditorsLoadAsync(activeTeIds, ct);
            var selectedTe = activeTE
                .Select(te => new { Editor = te, Load = loads.GetValueOrDefault(te.Id, 0) })
                .OrderBy(x => x.Load).ThenBy(x => x.Editor.CreatedAt)
                .Select(x => x.Editor)
                .First();

            var series = MangaSeries.Create(
                authorId:      submission.SubmitterId,
                submissionId:  submission.Id,
                title:         submission.Title,
                description:   submission.Description,
                genre:         submission.Genre,
                coverImageUrl: submission.CoverImageUrl);

            var mangaka = await _userRepo.GetByIdAsync(submission.SubmitterId, ct)
                ?? throw new InvalidOperationException($"Mangaka {submission.SubmitterId} not found.");
            mangaka.ManagingTantouId = selectedTe.Id;

            await _seriesRepo.AddAsync(series, ct);
            await _userRepo.UpdateAsync(mangaka, ct);

            seriesId = series.Id;
            selectedTeId = selectedTe.Id;

            await tx.CommitAsync(ct);
        });

        await _notificationService.NotifySubmissionApprovedAsync(
            receiverId:   submission.SubmitterId,
            submissionId: submission.Id,
            seriesId:     seriesId,
            seriesTitle:  submission.Title,
            ct:           ct);

        // [Mốc 5] Notify Tantou Editor được gán phụ trách sau phán quyết của Tổng biên tập
        var mangakaUser = await _userRepo.GetByIdAsync(submission.SubmitterId, ct);
        await _notificationService.NotifyTantouEditorAssignedAsync(
            tantouEditorId: selectedTeId,
            submissionId:   submission.Id,
            seriesTitle:    submission.Title,
            authorName:     mangakaUser?.FullName ?? "Không rõ",
            ct:             ct);
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class ResolveConflictValidator : AbstractValidator<ResolveConflictCommand>
{
    public ResolveConflictValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.EicId).NotEmpty();
        RuleFor(x => x.FeedbackMessage)
            .NotEmpty()
            .WithMessage("feedbackMessage là bắt buộc khi phân xử tranh chấp.")
            .MaximumLength(2000);
    }
}
