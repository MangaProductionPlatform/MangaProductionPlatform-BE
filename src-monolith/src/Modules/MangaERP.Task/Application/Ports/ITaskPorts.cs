using MangaERP.Shared.Domain.Entities;
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

public interface ITaskAssignmentAttemptRepository
{
    System.Threading.Tasks.Task<TaskAssignmentAttempt?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskAssignmentAttempt?> GetPendingByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<TaskAssignmentAttempt?> GetAcceptedByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<TaskAssignmentAttempt>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<TaskAssignmentAttempt>> GetPendingByCollaborationIdAsync(Guid collaborationId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<TaskAssignmentAttempt>> GetAcceptedByAssistantIdAsync(Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> GetMaxAttemptNumberAsync(Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> GetActiveWorkloadCountAsync(Guid assistantId, CancellationToken ct = default, Guid? excludeTaskId = null);
    System.Threading.Tasks.Task AddAsync(TaskAssignmentAttempt attempt, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(TaskAssignmentAttempt attempt, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ITaskProgressRepository
{
    System.Threading.Tasks.Task AddAsync(TaskProgressUpdate update, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<TaskProgressUpdate>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ITaskCheckpointRepository
{
    System.Threading.Tasks.Task AddAsync(TaskCheckpoint checkpoint, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<TaskCheckpoint>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IAuditEventRepository
{
    System.Threading.Tasks.Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}
