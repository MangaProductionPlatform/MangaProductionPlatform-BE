using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class ChapterRepository : IChapterRepository, IPageTaskRepository
{
    private readonly AppDbContext _db;

    public ChapterRepository(IDbContextProvider provider)
    {
        _db = (AppDbContext)provider.GetDbContext();
    }

    // ── IChapterRepository Implementation ──────────────────────────

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
                .ThenInclude(p => p.PreviewPage)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async System.Threading.Tasks.Task<IEnumerable<ChapterEntity>> GetScheduledChaptersAsync(DateTime threshold, CancellationToken ct = default)
        => await _db.Chapters
            .Where(c => c.Status == ChapterStatus.Approved &&
                        c.ScheduledPublishAt.HasValue &&
                        c.ScheduledPublishAt.Value <= threshold)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddAsync(ChapterEntity chapter, CancellationToken ct = default)
    {
        await _db.Chapters.AddAsync(chapter, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task UpdateAsync(ChapterEntity chapter, CancellationToken ct = default)
    {
        _db.Chapters.Update(chapter);
        await _db.SaveChangesAsync(ct);
    }

    // ── IPageTaskRepository Implementation ─────────────────────────

    async System.Threading.Tasks.Task<PageTask?> IPageTaskRepository.GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.PageTasks.FirstOrDefaultAsync(p => p.Id == id, ct);

    async System.Threading.Tasks.Task<IEnumerable<PageTask>> IPageTaskRepository.GetByChapterIdAsync(Guid chapterId, CancellationToken ct)
        => await _db.PageTasks
            .Where(p => p.ChapterId == chapterId)
            .OrderBy(p => p.PageNumber)
            .ToListAsync(ct);

    async System.Threading.Tasks.Task<IEnumerable<PageTask>> IPageTaskRepository.GetByAssistantAsync(Guid assistantId, CancellationToken ct)
        => await _db.PageTasks
            .Where(p => p.AssignedAssistantId == assistantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    async System.Threading.Tasks.Task<int> IPageTaskRepository.CountApprovedPagesAsync(Guid chapterId, CancellationToken ct)
        => await _db.PageTasks
            .CountAsync(p => p.ChapterId == chapterId && p.TaskStatus == PageTaskStatus.Approved, ct);

    async System.Threading.Tasks.Task IPageTaskRepository.AddAsync(PageTask pageTask, CancellationToken ct)
    {
        await _db.PageTasks.AddAsync(pageTask, ct);
        await _db.SaveChangesAsync(ct);
    }

    async System.Threading.Tasks.Task IPageTaskRepository.UpdateAsync(PageTask pageTask, CancellationToken ct)
    {
        _db.PageTasks.Update(pageTask);
        await _db.SaveChangesAsync(ct);
    }

    async System.Threading.Tasks.Task IPageTaskRepository.DeleteAsync(PageTask pageTask, CancellationToken ct)
    {
        _db.PageTasks.Remove(pageTask);
        await _db.SaveChangesAsync(ct);
    }
}
