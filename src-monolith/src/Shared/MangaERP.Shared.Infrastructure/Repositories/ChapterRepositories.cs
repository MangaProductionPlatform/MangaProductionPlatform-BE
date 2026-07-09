using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class ChapterRepository : IChapterRepository
{
    private readonly AppDbContext _db;

    public ChapterRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<ChapterEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Chapters.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async System.Threading.Tasks.Task<IEnumerable<ChapterEntity>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default)
        => await _db.Chapters
            .Where(c => c.SeriesId == seriesId)
            .OrderBy(c => c.ChapterNumber)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<ChapterEntity?> GetWithPagesAsync(Guid id, CancellationToken ct = default)
        => await _db.Chapters
            .Include(c => c.PageTasks)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async System.Threading.Tasks.Task<ChapterEntity?> GetWithPagesAndLayersAsync(Guid id, CancellationToken ct = default)
        => await _db.Chapters
            .Include(c => c.PageTasks)
                .ThenInclude(p => p.PreviewPage)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async System.Threading.Tasks.Task AddAsync(ChapterEntity chapter, CancellationToken ct = default)
        => await _db.Chapters.AddAsync(chapter, ct);

    public System.Threading.Tasks.Task UpdateAsync(ChapterEntity chapter, CancellationToken ct = default)
    {
        _db.Chapters.Update(chapter);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<ChapterEntity>> GetScheduledChaptersAsync(DateTime threshold, CancellationToken ct = default)
        => await _db.Chapters
            .Where(c => c.Status == ChapterStatus.Approved &&
                        c.ScheduledPublishAt.HasValue &&
                        c.ScheduledPublishAt.Value <= threshold)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<ChapterEntity>> GetQAQueueAsync(Guid editorId, CancellationToken ct = default)
        => await _db.Chapters
            .Where(c => c.Status == ChapterStatus.ReadyForQA && c.AssignedEditorId == editorId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<ChapterEntity>> GetApprovedChaptersAsync(bool? scheduledOnly, CancellationToken ct = default)
    {
        var query = _db.Chapters.Where(c => c.Status == ChapterStatus.Approved);

        if (scheduledOnly == true)
            query = query.Where(c => c.ScheduledPublishAt.HasValue);
        else if (scheduledOnly == false)
            query = query.Where(c => !c.ScheduledPublishAt.HasValue);

        return await query.OrderBy(c => c.ScheduledPublishAt ?? DateTime.MaxValue)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);
    }
}

public class PageTaskRepository : IPageTaskRepository
{
    private readonly AppDbContext _db;

    public PageTaskRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<PageTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.PageTasks
            .Include(p => p.PreviewPage)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async System.Threading.Tasks.Task<PageTask?> GetByChapterAndPageNumberAsync(
        Guid chapterId, int pageNumber, CancellationToken ct = default)
        => await _db.PageTasks
            .IgnoreQueryFilters() // Bỏ qua soft-delete filter để tránh unique index violation khi trang đã bị xóa mềm
            .FirstOrDefaultAsync(p => p.ChapterId == chapterId && p.PageNumber == pageNumber, ct);

    public async System.Threading.Tasks.Task<IEnumerable<PageTask>> GetByChapterIdAsync(Guid chapterId, CancellationToken ct = default)
        => await _db.PageTasks
            .Include(p => p.PreviewPage)
            .Where(p => p.ChapterId == chapterId)
            .OrderBy(p => p.PageNumber)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<PageTask>> GetByAssistantAsync(Guid assistantId, CancellationToken ct = default)
        => await _db.PageTasks
            .Include(p => p.Chapter)
            .Where(p => p.AssignedAssistantId == assistantId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<int> CountApprovedPagesAsync(Guid chapterId, CancellationToken ct = default)
        => await _db.PageTasks
            .CountAsync(p => p.ChapterId == chapterId && p.TaskStatus == PageTaskStatus.Approved, ct);

    public async System.Threading.Tasks.Task AddAsync(PageTask pageTask, CancellationToken ct = default)
        => await _db.PageTasks.AddAsync(pageTask, ct);

    public System.Threading.Tasks.Task UpdateAsync(PageTask pageTask, CancellationToken ct = default)
    {
        _db.PageTasks.Update(pageTask);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task DeleteAsync(PageTask pageTask, CancellationToken ct = default)
    {
        _db.PageTasks.Remove(pageTask);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}

public class PreviewPageRepository : IPreviewPageRepository
{
    private readonly AppDbContext _db;

    public PreviewPageRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<PreviewPage?> GetByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default)
        => await _db.PreviewPages.FirstOrDefaultAsync(p => p.PageTaskId == pageTaskId, ct);

    public async System.Threading.Tasks.Task AddAsync(PreviewPage previewPage, CancellationToken ct = default)
        => await _db.PreviewPages.AddAsync(previewPage, ct);

    public System.Threading.Tasks.Task UpdateAsync(PreviewPage previewPage, CancellationToken ct = default)
    {
        _db.PreviewPages.Update(previewPage);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
