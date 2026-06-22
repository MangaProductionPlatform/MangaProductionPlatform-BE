using MediatR;
using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Chapter.Application.Commands;

// ── Notification (Event) for QA module to catch ──────────────────────────────
public record ChapterSubmittedForQANotification(Guid ChapterId, Guid? AssignedEditorId) : INotification;

// ── Command & Result ──────────────────────────────────────────────────────────
public record SubmitForQACommand(Guid ChapterId, Guid UserId) : IRequest<SubmitForQAResult>;

public record SubmitForQAResult(Guid ChapterId, string Status, DateTime SubmittedAt);

// ── Handler ───────────────────────────────────────────────────────────────────
public class SubmitForQAHandler : IRequestHandler<SubmitForQACommand, SubmitForQAResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IPublisher _publisher; // MediatR IPublisher to broadcast notification

    public SubmitForQAHandler(
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IPublisher publisher)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _publisher = publisher;
    }

    public async Task<SubmitForQAResult> Handle(SubmitForQACommand request, CancellationToken cancellationToken)
    {
        // 1. Get Chapter
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        // 2. Get Series to check ownership
        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        if (series.AuthorId != request.UserId)
            throw new UnauthorizedAccessException("Bạn không phải là tác giả (Mangaka) của bộ truyện này.");

        // 3. Domain logic transition
        chapter.SubmitForQA();
        await _chapterRepo.UpdateAsync(chapter, cancellationToken);

        // 4. Publish in-process notification for QA Module to create QASession
        await _publisher.Publish(new ChapterSubmittedForQANotification(chapter.Id, chapter.AssignedEditorId), cancellationToken);

        return new SubmitForQAResult(chapter.Id, chapter.Status.ToString(), DateTime.UtcNow);
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────
public class SubmitForQAValidator : AbstractValidator<SubmitForQACommand>
{
    public SubmitForQAValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
