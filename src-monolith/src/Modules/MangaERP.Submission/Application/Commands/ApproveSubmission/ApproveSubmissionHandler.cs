using FluentValidation;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Submission.Application.Commands.ApproveSubmission;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Editorial Board duyệt submission: RecommendedToBoard → Approved.
/// Đồng thời tạo MangaSeries trong cùng một DB transaction.
/// ReviewerId được controller trích từ JWT claim.
/// </summary>
public record ApproveSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewerId          // extracted from JWT by controller (EditorialBoard member)
) : IRequest<ApproveSubmissionResult>;

public record ApproveSubmissionResult(
    Guid SubmissionId,
    Guid SeriesId,
    string SubmissionStatus,
    string SeriesStatus,
    DateTime ApprovedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class ApproveSubmissionHandler
    : IRequestHandler<ApproveSubmissionCommand, ApproveSubmissionResult>
{
    private readonly ISubmissionRepository _submissionRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IDbContextProvider _dbContextProvider;

    public ApproveSubmissionHandler(
        ISubmissionRepository submissionRepo,
        ISeriesRepository seriesRepo,
        IDbContextProvider dbContextProvider)
    {
        _submissionRepo = submissionRepo;
        _seriesRepo = seriesRepo;
        _dbContextProvider = dbContextProvider;
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
                "EB_Approved",
                existingSeries.Status.ToString(),
                existingSeries.CreatedAt);

        // ── Load submission ───────────────────────────────────────────────────
        var submission = await _submissionRepo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // ── Atomic transaction ────────────────────────────────────────────────
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Domain: RecommendedToBoard → Approved
            submission.Approve(cmd.ReviewerId);

            // 2. Create MangaSeries linked to this submission and Mangaka
            var series = MangaSeries.Create(
                authorId:      submission.SubmitterId,
                submissionId:  submission.Id,
                title:         submission.Title,
                description:   submission.Description,
                genre:         submission.Genre,
                coverImageUrl: submission.CoverImageUrl);

            // 3. Persist both in the same transaction
            await _seriesRepo.AddAsync(series, ct);
            await _submissionRepo.SaveChangesAsync(ct);
            // Note: SaveChangesAsync on one repo saves all tracked changes in AppDbContext

            await tx.CommitAsync(ct);

            return new ApproveSubmissionResult(
                submission.Id,
                series.Id,
                submission.Status.ToString(),
                series.Status.ToString(),
                submission.ReviewedAt!.Value);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class ApproveSubmissionValidator : AbstractValidator<ApproveSubmissionCommand>
{
    public ApproveSubmissionValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
    }
}
