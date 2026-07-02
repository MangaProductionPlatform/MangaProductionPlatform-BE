using MangaERP.Segmentation.Domain.Entities;

namespace MangaERP.Segmentation.Application.Ports;

public interface ISegmentationTaskRepository
{
    Task<SegmentationTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    
    Task<(IEnumerable<SegmentationTask> Items, int TotalCount)> GetByAssignedUserAsync(
        Guid assignedToUserId,
        SegmentationTaskStatus? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default);
        
    Task AddAsync(SegmentationTask task, CancellationToken ct = default);
    
    Task UpdateAsync(SegmentationTask task, CancellationToken ct = default);
    
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
