using MediatR;
using FluentValidation;
using MangaERP.QA.Application.Ports;
using MangaERP.QA.Domain.Entities;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.QA.Application.Commands.StartQaReview;

// ── Command & Result ──────────────────────────────────────────────────────────
public record StartQaReviewCommand(Guid ChapterId, Guid EditorId) : IRequest<StartQaReviewResult>;

public record StartQaReviewResult(
    Guid SessionId,
    Guid ChapterId,
    Guid EditorId,
    string Status,
    DateTime StartedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────
public class StartQaReviewHandler : IRequestHandler<StartQaReviewCommand, StartQaReviewResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IQASessionRepository _qaSessionRepo;

    public StartQaReviewHandler(
        IChapterRepository chapterRepo,
        IQASessionRepository qaSessionRepo)
    {
        _chapterRepo = chapterRepo;
        _qaSessionRepo = qaSessionRepo;
    }

    public async Task<StartQaReviewResult> Handle(StartQaReviewCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Chapter state
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (chapter.Status != ChapterStatus.ReadyForQA)
            throw new InvalidOperationException("Chỉ có thể bắt đầu QA review cho chương truyện đang ở trạng thái ReadyForQA.");

        // 2. Check for existing active session
        var existingSession = await _qaSessionRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);

        if (existingSession is not null && existingSession.Status == "InProgress")
        {
            // If same editor, return existing session (idempotent)
            if (existingSession.EditorId == request.EditorId)
            {
                return new StartQaReviewResult(
                    existingSession.Id,
                    existingSession.ChapterId,
                    existingSession.EditorId,
                    existingSession.Status,
                    existingSession.CreatedAt
                );
            }

            throw new InvalidOperationException(
                "Chương truyện này đang được editor khác review. Không thể bắt đầu review song song.");
        }

        // 3. Create or reset session
        if (existingSession is not null)
        {
            // Reset completed session for new round
            existingSession.EditorId = request.EditorId;
            existingSession.Status = "InProgress";
            existingSession.IsApproved = false;
            existingSession.ApprovedAt = null;
            existingSession.CompletedAt = null;
            await _qaSessionRepo.UpdateAsync(existingSession, cancellationToken);

            return new StartQaReviewResult(
                existingSession.Id,
                existingSession.ChapterId,
                existingSession.EditorId,
                existingSession.Status,
                existingSession.CreatedAt
            );
        }

        // Create new session
        var newSession = new QASession
        {
            ChapterId = request.ChapterId,
            EditorId = request.EditorId,
            Status = "InProgress",
            IsApproved = false,
            CreatedAt = DateTime.UtcNow
        };
        await _qaSessionRepo.AddAsync(newSession, cancellationToken);

        return new StartQaReviewResult(
            newSession.Id,
            newSession.ChapterId,
            newSession.EditorId,
            newSession.Status,
            newSession.CreatedAt
        );
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────
public class StartQaReviewValidator : AbstractValidator<StartQaReviewCommand>
{
    public StartQaReviewValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
    }
}
