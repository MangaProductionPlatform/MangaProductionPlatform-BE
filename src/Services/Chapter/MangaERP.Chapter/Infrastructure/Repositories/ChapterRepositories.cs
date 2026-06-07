using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Chapter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Chapter.Infrastructure.Repositories;

public class ChapterRepository : IChapterRepository
{
    private readonly ChapterDbContext _context;

    public ChapterRepository(ChapterDbContext context) => _context = context;

    public async Task<ChapterEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Chapters.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IEnumerable<ChapterEntity>> GetBySeriesIdAsync(Guid seriesId, CancellationToken cancellationToken = default)
        => await _context.Chapters
            .Where(c => c.SeriesId == seriesId)
            .OrderBy(c => c.ChapterNumber)
            .ToListAsync(cancellationToken);

    public async Task<ChapterEntity?> GetWithPagesAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Chapters
            .Include(c => c.PageTasks)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task AddAsync(ChapterEntity chapter, CancellationToken cancellationToken = default)
    {
        await _context.Chapters.AddAsync(chapter, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ChapterEntity chapter, CancellationToken cancellationToken = default)
    {
        _context.Chapters.Update(chapter);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class PageTaskRepository : IPageTaskRepository
{
    private readonly ChapterDbContext _context;

    public PageTaskRepository(ChapterDbContext context) => _context = context;

    public async Task<PageTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.PageTasks.FirstOrDefaultAsync(pt => pt.Id == id, cancellationToken);

    public async Task<IEnumerable<PageTask>> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default)
        => await _context.PageTasks
            .Where(pt => pt.ChapterId == chapterId)
            .OrderBy(pt => pt.PageNumber)
            .ToListAsync(cancellationToken);

    public async Task<int> CountApprovedPagesAsync(Guid chapterId, CancellationToken cancellationToken = default)
        => await _context.PageTasks
            .CountAsync(pt => pt.ChapterId == chapterId && pt.TaskStatus == PageTaskStatus.Approved, cancellationToken);

    public async Task AddAsync(PageTask pageTask, CancellationToken cancellationToken = default)
    {
        await _context.PageTasks.AddAsync(pageTask, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PageTask pageTask, CancellationToken cancellationToken = default)
    {
        _context.PageTasks.Update(pageTask);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PageTask pageTask, CancellationToken cancellationToken = default)
    {
        _context.PageTasks.Remove(pageTask);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
