using MangaERP.Segmentation.Application.Ports;
using MangaERP.Segmentation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Segmentation.Infrastructure.Repositories;

public class SegmentationTaskRepository : ISegmentationTaskRepository
{
    private readonly SegmentationDbContext _db;

    public SegmentationTaskRepository(SegmentationDbContext db)
    {
        _db = db;
    }

    public async Task<SegmentationTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SegmentationTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<(IEnumerable<SegmentationTask> Items, int TotalCount)> GetByAssignedUserAsync(
        Guid assignedToUserId,
        SegmentationTaskStatus? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.SegmentationTasks
            .Where(t => t.AssignedToUserId == assignedToUserId);

        if (statusFilter.HasValue)
            query = query.Where(t => t.Status == statusFilter.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(SegmentationTask task, CancellationToken ct = default)
        => await _db.SegmentationTasks.AddAsync(task, ct);

    public Task UpdateAsync(SegmentationTask task, CancellationToken ct = default)
    {
        _db.SegmentationTasks.Update(task);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
