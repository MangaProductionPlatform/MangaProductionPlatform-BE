using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Chapter.Application.Ports;

public interface IChapterRepository
{
    Task<ChapterEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ChapterEntity>> GetBySeriesIdAsync(Guid seriesId, CancellationToken cancellationToken = default);
    Task<ChapterEntity?> GetWithPagesAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(ChapterEntity chapter, CancellationToken cancellationToken = default);
    Task UpdateAsync(ChapterEntity chapter, CancellationToken cancellationToken = default);
}

public interface IPageTaskRepository
{
    Task<PageTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PageTask>> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task<int> CountApprovedPagesAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task AddAsync(PageTask pageTask, CancellationToken cancellationToken = default);
    Task UpdateAsync(PageTask pageTask, CancellationToken cancellationToken = default);
    Task DeleteAsync(PageTask pageTask, CancellationToken cancellationToken = default);
}
