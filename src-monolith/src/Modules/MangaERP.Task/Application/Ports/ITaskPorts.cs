using MangaERP.Task.Domain.Entities;

namespace MangaERP.Task.Application.Ports;

public interface IArtworkLayerRepository
{
    System.Threading.Tasks.Task<ArtworkLayer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<ArtworkLayer?> GetCurrentByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<ArtworkLayer>> GetByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> GetMaxVersionAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(ArtworkLayer layer, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(ArtworkLayer layer, CancellationToken ct = default);
    System.Threading.Tasks.Task MarkPreviousVersionsNotCurrentAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ITaskCommentRepository
{
    System.Threading.Tasks.Task<IEnumerable<TaskComment>> GetByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(TaskComment comment, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IDeadlineExtensionRequestRepository
{
    System.Threading.Tasks.Task<DeadlineExtensionRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<DeadlineExtensionRequest>> GetByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<DeadlineExtensionRequest>> GetByAssistantIdAsync(Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> HasPendingRequestAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(DeadlineExtensionRequest request, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}
