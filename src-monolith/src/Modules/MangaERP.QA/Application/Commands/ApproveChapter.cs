using MediatR;
using FluentValidation;
using MangaERP.QA.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.QA.Application.Commands;

// ── Notification (Event) for other modules to catch ─────────────────────────
public record ChapterApprovedNotification(Guid ChapterId, Guid EditorId) : INotification;

// ── Command & Result ──────────────────────────────────────────────────────────
public record ApproveChapterCommand(Guid ChapterId, Guid EditorId) : IRequest<ApproveChapterResult>;

public record ApproveChapterResult(Guid ChapterId, string Status, DateTime ApprovedAt);

// ── Handler ───────────────────────────────────────────────────────────────────
public class ApproveChapterHandler : IRequestHandler<ApproveChapterCommand, ApproveChapterResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IQASessionRepository _qaSessionRepo;
    private readonly IPublisher _publisher;

    public ApproveChapterHandler(
        IChapterRepository chapterRepo,
        IBugPinRepository bugPinRepo,
        IQASessionRepository qaSessionRepo,
        IPublisher publisher)
    {
        _chapterRepo = chapterRepo;
        _bugPinRepo = bugPinRepo;
        _qaSessionRepo = qaSessionRepo;
        _publisher = publisher;
    }

    public async Task<ApproveChapterResult> Handle(ApproveChapterCommand request, CancellationToken cancellationToken)
    {
        // 1. Load Chapter
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (chapter.Status == ChapterStatus.Approved)
            return new ApproveChapterResult(chapter.Id, chapter.Status.ToString(), DateTime.UtcNow);

        if (chapter.Status != ChapterStatus.ReadyForQA)
            throw new InvalidOperationException("Chỉ có thể phê duyệt chương truyện ở trạng thái ReadyForQA.");

        // 2. Load Bug Pins and ensure all are resolved
        var pins = await _bugPinRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        if (pins.Any(p => p.Status != "Resolved"))
            throw new InvalidOperationException("Không thể phê duyệt chương truyện khi vẫn còn ghim lỗi chưa sửa (Status khác Resolved).");

        // 3. Update QA Session
        var session = await _qaSessionRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"QASession for chapter {request.ChapterId} not found.");

        session.Status = "Completed";
        session.IsApproved = true;
        session.ApprovedAt = DateTime.UtcNow;
        session.CompletedAt = DateTime.UtcNow;
        await _qaSessionRepo.UpdateAsync(session, cancellationToken);

        // 4. Update Chapter Status
        chapter.Approve();
        await _chapterRepo.UpdateAsync(chapter, cancellationToken);

        // 5. Publish event
        await _publisher.Publish(new ChapterApprovedNotification(chapter.Id, request.EditorId), cancellationToken);

        return new ApproveChapterResult(chapter.Id, chapter.Status.ToString(), session.ApprovedAt.Value);
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────
public class ApproveChapterValidator : AbstractValidator<ApproveChapterCommand>
{
    public ApproveChapterValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
    }
}
