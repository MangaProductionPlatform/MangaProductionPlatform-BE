using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public sealed class TaskAssignmentRepository : ITaskAssignmentAttemptRepository
{
    private readonly AppDbContext _db;

    public TaskAssignmentRepository(IDbContextProvider provider)
    {
        _db = (AppDbContext)provider.GetDbContext();
    }

    public async System.Threading.Tasks.Task<TaskAssignmentAttempt?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.TaskAssignmentAttempts.FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async System.Threading.Tasks.Task<TaskAssignmentAttempt?> GetPendingByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _db.TaskAssignmentAttempts
            .FirstOrDefaultAsync(a => a.TaskId == taskId && a.Status == TaskAssignmentAttemptStatus.PendingAcceptance, ct);
    }

    public async System.Threading.Tasks.Task<TaskAssignmentAttempt?> GetAcceptedByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _db.TaskAssignmentAttempts
            .FirstOrDefaultAsync(a => a.TaskId == taskId && a.Status == TaskAssignmentAttemptStatus.Accepted, ct);
    }

    public async System.Threading.Tasks.Task<IEnumerable<TaskAssignmentAttempt>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _db.TaskAssignmentAttempts
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.AttemptNumber)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task<IEnumerable<TaskAssignmentAttempt>> GetPendingByCollaborationIdAsync(Guid collaborationId, CancellationToken ct = default)
    {
        return await _db.TaskAssignmentAttempts
            .Where(a => a.CollaborationId == collaborationId && a.Status == TaskAssignmentAttemptStatus.PendingAcceptance)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task<IEnumerable<TaskAssignmentAttempt>> GetAcceptedByAssistantIdAsync(Guid assistantId, CancellationToken ct = default)
    {
        return await _db.TaskAssignmentAttempts
            .Where(a => a.AssistantId == assistantId && a.Status == TaskAssignmentAttemptStatus.Accepted)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task<int> GetMaxAttemptNumberAsync(Guid taskId, CancellationToken ct = default)
    {
        var attempts = await _db.TaskAssignmentAttempts
            .Where(a => a.TaskId == taskId)
            .Select(a => a.AttemptNumber)
            .ToListAsync(ct);

        return attempts.Count != 0 ? attempts.Max() : 0;
    }

    public async System.Threading.Tasks.Task<int> GetActiveWorkloadCountAsync(Guid assistantId, CancellationToken ct = default, Guid? excludeTaskId = null)
    {
        return await _db.PageTasks.AsNoTracking()
            .Where(t => t.AssignedAssistantId == assistantId)
            .Where(t => excludeTaskId == null || t.Id != excludeTaskId.Value)
            .Where(t => t.TaskStatus != PageTaskStatus.Approved && t.TaskStatus != PageTaskStatus.Cancelled && !t.IsDeleted)
            .CountAsync(ct);
    }

    public async System.Threading.Tasks.Task AddAsync(TaskAssignmentAttempt attempt, CancellationToken ct = default)
    {
        await _db.TaskAssignmentAttempts.AddAsync(attempt, ct);
    }

    public System.Threading.Tasks.Task UpdateAsync(TaskAssignmentAttempt attempt, CancellationToken ct = default)
    {
        _db.TaskAssignmentAttempts.Update(attempt);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
