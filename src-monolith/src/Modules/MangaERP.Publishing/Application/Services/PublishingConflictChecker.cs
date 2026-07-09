using MangaERP.Chapter.Application.Ports;

namespace MangaERP.Publishing.Application.Services;

public interface IPublishingConflictChecker
{
    Task<ConflictCheckResult> CheckAsync(Guid seriesId, DateTime scheduledAt, Guid? excludeChapterId, CancellationToken ct = default);
}

public record ConflictCheckResult(bool HasConflict, string? ConflictMessage, Guid? ConflictingChapterId);

public class PublishingConflictChecker : IPublishingConflictChecker
{
    private readonly IChapterRepository _chapterRepo;

    public PublishingConflictChecker(IChapterRepository chapterRepo)
    {
        _chapterRepo = chapterRepo;
    }

    public async Task<ConflictCheckResult> CheckAsync(Guid seriesId, DateTime scheduledAt, Guid? excludeChapterId, CancellationToken ct = default)
    {
        var scheduledChapters = await _chapterRepo.GetScheduledBySeriesAsync(seriesId, ct);

        // Check if there's any chapter scheduled on the same date for the same series
        var conflictingChapter = scheduledChapters.FirstOrDefault(c =>
            c.Id != excludeChapterId &&
            c.ScheduledPublishAt.HasValue &&
            c.ScheduledPublishAt.Value.Date == scheduledAt.Date);

        if (conflictingChapter != null)
        {
            return new ConflictCheckResult(
                true,
                $"Đã có chương truyện ({conflictingChapter.ChapterNumber}) được lên lịch phát hành vào ngày {scheduledAt:dd/MM/yyyy}. Vui lòng chọn ngày khác để tránh trùng lặp.",
                conflictingChapter.Id);
        }

        return new ConflictCheckResult(false, null, null);
    }
}
