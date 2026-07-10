using MediatR;
using FluentValidation;
using MangaERP.QA.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.QA.Application.Commands;

// ── Notification (Event) for Publishing module to catch and create db Notification ────────
public record FeedbackBatchSentNotification(Guid ChapterId, Guid MangakaUserId, Guid BatchToken) : INotification;

// ── Command & Result ──────────────────────────────────────────────────────────
public record SendFeedbackBatchCommand(Guid ChapterId, Guid EditorId, Guid BatchToken) : IRequest<SendFeedbackBatchResult>;

public record SendFeedbackBatchResult(Guid ChapterId, string Status, Guid BatchToken, DateTime SentAt);

// ── Handler ───────────────────────────────────────────────────────────────────
public class SendFeedbackBatchHandler : IRequestHandler<SendFeedbackBatchCommand, SendFeedbackBatchResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IPublisher _publisher;

    public SendFeedbackBatchHandler(
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IBugPinRepository bugPinRepo,
        IPublisher publisher)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _bugPinRepo = bugPinRepo;
        _publisher = publisher;
    }

    public async Task<SendFeedbackBatchResult> Handle(SendFeedbackBatchCommand request, CancellationToken cancellationToken)
    {
        // 1. Get Chapter
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (chapter.AssignedEditorId != request.EditorId)
            throw new UnauthorizedAccessException("Bạn không phải Tantou Editor được giao cho chương truyện này.");

        if (chapter.Status != ChapterStatus.ReadyForQA)
            throw new InvalidOperationException("Chỉ có thể gửi phản hồi cho chương truyện đang trong trạng thái ReadyForQA.");

        // 2. Load Series to get Mangaka ID
        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // 3. Validate and update the Bug Pins status associated with this batch token
        var pins = await _bugPinRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        var batchPins = pins
            .Where(p => p.BatchToken == request.BatchToken && p.Status == "Open")
            .ToList();

        if (!batchPins.Any())
            throw new InvalidOperationException("Không thể gửi phản hồi vì batch không có ghim lỗi Open nào.");

        foreach (var pin in batchPins)
        {
            pin.Status = "InFixing";
            await _bugPinRepo.UpdateAsync(pin, cancellationToken);
        }

        // 4. Update chapter status after confirming a valid feedback batch.
        chapter.RequestQaRevision();
        await _chapterRepo.UpdateAsync(chapter, cancellationToken);

        // 5. Publish event to alert Mangaka (via Notification system)
        await _publisher.Publish(new FeedbackBatchSentNotification(chapter.Id, series.AuthorId, request.BatchToken), cancellationToken);

        return new SendFeedbackBatchResult(chapter.Id, chapter.Status.ToString(), request.BatchToken, DateTime.UtcNow);
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────
public class SendFeedbackBatchValidator : AbstractValidator<SendFeedbackBatchCommand>
{
    public SendFeedbackBatchValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
        RuleFor(x => x.BatchToken).NotEmpty();
    }
}
