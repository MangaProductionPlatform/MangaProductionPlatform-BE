using FluentValidation;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Submission.Application.Commands.ApproveSubmission;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Editorial Board duyệt submission: Pending_EB_Review → EB_Approved.
/// Đồng thời tạo MangaSeries và gán Tantou Editor phụ trách trong cùng một DB transaction.
/// ReviewerId được controller trích từ JWT claim.
/// </summary>
public record ApproveSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewerId,         // extracted from JWT by controller (EditorialBoard member)
    Guid AssignedEditorId    // Tantou Editor được chỉ định phụ trách Series mới
) : IRequest<ApproveSubmissionResult>;

public record ApproveSubmissionResult(
    Guid SubmissionId,
    Guid SeriesId,
    Guid AssignedEditorId,
    string SubmissionStatus,
    string SeriesStatus,
    DateTime ApprovedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class ApproveSubmissionHandler
    : IRequestHandler<ApproveSubmissionCommand, ApproveSubmissionResult>
{
    private readonly ISubmissionRepository _submissionRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;
    private readonly IDbContextProvider _dbContextProvider;
    private readonly INotificationService _notificationService;

    public ApproveSubmissionHandler(
        ISubmissionRepository submissionRepo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo,
        IDbContextProvider dbContextProvider,
        INotificationService notificationService)
    {
        _submissionRepo = submissionRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
        _dbContextProvider = dbContextProvider;
        _notificationService = notificationService;
    }

    public async Task<ApproveSubmissionResult> Handle(
        ApproveSubmissionCommand cmd, CancellationToken ct)
    {
        var db = (DbContext)_dbContextProvider.GetDbContext();

        // ── Idempotency guard ─────────────────────────────────────────────────
        var existingSeries = await _seriesRepo.GetBySubmissionIdAsync(cmd.SubmissionId, ct);
        if (existingSeries is not null)
            return new ApproveSubmissionResult(
                cmd.SubmissionId,
                existingSeries.Id,
                cmd.AssignedEditorId,
                "EB_Approved",
                existingSeries.Status.ToString(),
                existingSeries.CreatedAt);

        // ── Load & validate submission ────────────────────────────────────────
        var submission = await _submissionRepo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // ── Validate AssignedEditorId is a real TantouEditor ──────────────────
        var assignedEditor = await _userRepo.GetByIdAsync(cmd.AssignedEditorId, ct)
            ?? throw new InvalidOperationException($"User {cmd.AssignedEditorId} not found.");
        if (assignedEditor.Role != UserRole.TantouEditor)
            throw new InvalidOperationException(
                $"User {cmd.AssignedEditorId} is not a TantouEditor. Cannot assign as managing editor.");

        // ── Domain validation BEFORE opening the transaction ──────────────────
        submission.Approve(cmd.ReviewerId);

        // ── Atomic transaction via ExecutionStrategy ──────────────────────────
        // NpgsqlRetryingExecutionStrategy (EnableRetryOnFailure) không cho phép
        // BeginTransactionAsync trực tiếp. Phải wrap trong ExecutionStrategy.
        var strategy = db.Database.CreateExecutionStrategy();

        ApproveSubmissionResult? result = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // 1. Create MangaSeries linked to this submission and Mangaka
            var series = MangaSeries.Create(
                authorId:      submission.SubmitterId,
                submissionId:  submission.Id,
                title:         submission.Title,
                description:   submission.Description,
                genre:         submission.Genre,
                coverImageUrl: submission.CoverImageUrl);

            // 2. Gán Tantou Editor cho Mangaka
            var mangaka = await _userRepo.GetByIdAsync(submission.SubmitterId, ct)
                ?? throw new InvalidOperationException($"Mangaka {submission.SubmitterId} not found.");
            mangaka.ManagingTantouId = cmd.AssignedEditorId;

            // 3. Persist all changes in the same transaction
            await _seriesRepo.AddAsync(series, ct);
            await _userRepo.UpdateAsync(mangaka, ct);
            await _submissionRepo.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            result = new ApproveSubmissionResult(
                submission.Id,
                series.Id,
                cmd.AssignedEditorId,
                submission.Status.ToString(),
                series.Status.ToString(),
                submission.ReviewedAt!.Value);
        });

        // Send notification AFTER successful commit (outside transaction)
        await _notificationService.NotifySubmissionApprovedAsync(
            receiverId:   submission.SubmitterId,
            submissionId: submission.Id,
            seriesId:     result!.SeriesId,
            seriesTitle:  submission.Title,
            ct:           ct);

        return result!;
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class ApproveSubmissionValidator : AbstractValidator<ApproveSubmissionCommand>
{
    public ApproveSubmissionValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
        RuleFor(x => x.AssignedEditorId).NotEmpty()
            .WithMessage("Phải chỉ định Tantou Editor phụ trách Series.");
    }
}
