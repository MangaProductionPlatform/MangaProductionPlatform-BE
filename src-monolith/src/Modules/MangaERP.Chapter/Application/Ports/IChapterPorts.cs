using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Chapter.Application.Ports;

public interface IChapterRepository
{
    Task<ChapterEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ChapterEntity>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default);
    Task<ChapterEntity?> GetWithPagesAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ChapterEntity chapter, CancellationToken ct = default);
    Task UpdateAsync(ChapterEntity chapter, CancellationToken ct = default);
}

public interface IPageTaskRepository
{
    Task<PageTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<PageTask>> GetByChapterIdAsync(Guid chapterId, CancellationToken ct = default);
    Task<IEnumerable<PageTask>> GetByAssistantAsync(Guid assistantId, CancellationToken ct = default);
    Task<int> CountApprovedPagesAsync(Guid chapterId, CancellationToken ct = default);
    Task AddAsync(PageTask pageTask, CancellationToken ct = default);
    Task UpdateAsync(PageTask pageTask, CancellationToken ct = default);
    Task DeleteAsync(PageTask pageTask, CancellationToken ct = default);
}
