using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.SubmitForQA;

// ── Notification (Event) for QA module to catch ──────────────────────────────
public record ChapterSubmittedForQANotification(Guid ChapterId, Guid? AssignedEditorId) : INotification;

// ── Command & Result ──────────────────────────────────────────────────────────
public record SubmitChapterForQACommand(
    Guid MangakaId,
    Guid ChapterId
) : IRequest<SubmitChapterForQAResult>;

public record SubmitChapterForQAResult(Guid ChapterId, string Status);

// ── Handler ───────────────────────────────────────────────────────────────────
public class SubmitChapterForQAHandler : IRequestHandler<SubmitChapterForQACommand, SubmitChapterForQAResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly INotificationService _notificationService;
    private readonly IPublisher _publisher;

    public SubmitChapterForQAHandler(
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        INotificationService notificationService,
        IPublisher publisher)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _notificationService = notificationService;
        _publisher = publisher;
    }

    public async Task<SubmitChapterForQAResult> Handle(SubmitChapterForQACommand cmd, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetWithPagesAsync(cmd.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {cmd.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);
        chapter.SubmitForQA();

        await _chapterRepo.UpdateAsync(chapter, ct);
        await _chapterRepo.SaveChangesAsync(ct);

        // Notify editors that a chapter is ready for QA review
        await _notificationService.NotifyChapterReadyForQAAsync(chapter.Id, chapter.Title, ct);

        // Publish in-process MediatR event for QA Module to create QASession
        await _publisher.Publish(
            new ChapterSubmittedForQANotification(chapter.Id, chapter.AssignedEditorId), ct);

        return new SubmitChapterForQAResult(chapter.Id, chapter.Status.ToString());
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────
public class SubmitChapterForQAValidator : AbstractValidator<SubmitChapterForQACommand>
{
    public SubmitChapterForQAValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.ChapterId).NotEmpty();
    }
}
