using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Chapter.Application.Ports;

public interface IChapterRepository
{
    Task<ChapterEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ChapterEntity>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default);
    Task<ChapterEntity?> GetWithPagesAsync(Guid id, CancellationToken ct = default);
    Task<ChapterEntity?> GetWithPagesAndLayersAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ChapterEntity chapter, CancellationToken ct = default);
    Task UpdateAsync(ChapterEntity chapter, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IEnumerable<ChapterEntity>> GetScheduledChaptersAsync(DateTime threshold, CancellationToken ct = default);
    Task<IEnumerable<ChapterEntity>> GetQAQueueAsync(Guid editorId, CancellationToken ct = default);
    Task<IEnumerable<ChapterEntity>> GetApprovedChaptersAsync(bool? scheduledOnly, CancellationToken ct = default);
}

public interface IPageTaskRepository
{
    Task<PageTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PageTask?> GetByChapterAndPageNumberAsync(Guid chapterId, int pageNumber, CancellationToken ct = default);
    Task<IEnumerable<PageTask>> GetByChapterIdAsync(Guid chapterId, CancellationToken ct = default);
    Task<IEnumerable<PageTask>> GetByAssistantAsync(Guid assistantId, CancellationToken ct = default);
    Task<int> CountApprovedPagesAsync(Guid chapterId, CancellationToken ct = default);
    Task AddAsync(PageTask pageTask, CancellationToken ct = default);
    Task UpdateAsync(PageTask pageTask, CancellationToken ct = default);
    Task DeleteAsync(PageTask pageTask, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IPreviewPageRepository
{
    Task<PreviewPage?> GetByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default);
    Task AddAsync(PreviewPage previewPage, CancellationToken ct = default);
    Task UpdateAsync(PreviewPage previewPage, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
