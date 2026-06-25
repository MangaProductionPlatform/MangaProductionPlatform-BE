using MediatR;
using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Publishing.Domain.Entities;

namespace MangaERP.Publishing.Application.Commands;

public record PublishChapterCommand(
    Guid ChapterId,
    Guid? PublishedByUserId = null
) : IRequest<PublishChapterResult>;

public record PublishChapterResult(
    Guid ChapterId,
    string Status,
    string PublicationUrl,
    DateTime PublishedAt
);

public class PublishChapterHandler : IRequestHandler<PublishChapterCommand, PublishChapterResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPublicationRecordRepository _pubRecordRepo;

    public PublishChapterHandler(
        IChapterRepository chapterRepo,
        IPublicationRecordRepository pubRecordRepo)
    {
        _chapterRepo = chapterRepo;
        _pubRecordRepo = pubRecordRepo;
    }

    public async Task<PublishChapterResult> Handle(PublishChapterCommand request, CancellationToken cancellationToken)
    {
        // 1. Get Chapter with pages
        var chapter = await _chapterRepo.GetWithPagesAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        // Idempotency check
        var existingRecord = await _pubRecordRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        if (existingRecord is not null)
        {
            return new PublishChapterResult(
                chapter.Id,
                ChapterStatus.Published.ToString(),
                existingRecord.PublicationUrl ?? string.Empty,
                existingRecord.PublishedAt
            );
        }

        if (chapter.Status != ChapterStatus.Approved)
            throw new InvalidOperationException("Chỉ có thể xuất bản chương truyện đã được duyệt (Status = Approved).");

        // 2. Publish logic
        var issueType = chapter.IssueType ?? "Special";
        chapter.Publish(issueType);
        
        // 3. Mark preview pages as published
        foreach (var task in chapter.PageTasks)
        {
            if (task.PreviewPage is not null)
            {
                task.PreviewPage.IsPublished = true;
                task.PreviewPage.ProductionFileUrl = task.PreviewPage.CompositeFileUrl; // Simulated CDN upload
            }
        }
        await _chapterRepo.UpdateAsync(chapter, cancellationToken);

        // 4. Create PublicationRecord
        var pubUrl = $"https://company.com/reader/series/{chapter.SeriesId}/chapters/{chapter.Id}";
        var cacheKey = $"series:{chapter.SeriesId}:chapter:{chapter.Id}";

        var pubRecord = new PublicationRecord
        {
            ChapterId = chapter.Id,
            SeriesId = chapter.SeriesId,
            PublishedByUserId = request.PublishedByUserId,
            IssueType = issueType,
            PublicationUrl = pubUrl,
            CacheKey = cacheKey,
            PublishedAt = DateTime.UtcNow
        };
        await _pubRecordRepo.AddAsync(pubRecord, cancellationToken);

        // 5. Cache invalidation simulation (MF8)
        Console.WriteLine($"[CACHE EVICTION] Evicted cache key: {cacheKey}");

        // 6. Cold storage archiving simulation (optional/async)
        // chapter.Archive();
        // await _chapterRepo.UpdateAsync(chapter, cancellationToken);

        return new PublishChapterResult(
            chapter.Id,
            chapter.Status.ToString(),
            pubRecord.PublicationUrl,
            pubRecord.PublishedAt
        );
    }
}

public class PublishChapterValidator : AbstractValidator<PublishChapterCommand>
{
    public PublishChapterValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty();
    }
}
