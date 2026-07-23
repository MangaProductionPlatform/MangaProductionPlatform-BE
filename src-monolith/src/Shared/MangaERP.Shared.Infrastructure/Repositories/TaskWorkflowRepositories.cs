using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public sealed class TaskProgressRepository : ITaskProgressRepository
{
    private readonly AppDbContext _db;
    public TaskProgressRepository(IDbContextProvider provider) => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task AddAsync(TaskProgressUpdate update, CancellationToken ct = default)
    {
        await _db.TaskProgressUpdates.AddAsync(update, ct);
    }

    public async System.Threading.Tasks.Task<IEnumerable<TaskProgressUpdate>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _db.TaskProgressUpdates.AsNoTracking()
            .Where(p => p.TaskId == taskId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}

public sealed class TaskCheckpointRepository : ITaskCheckpointRepository
{
    private readonly AppDbContext _db;
    public TaskCheckpointRepository(IDbContextProvider provider) => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task AddAsync(TaskCheckpoint checkpoint, CancellationToken ct = default)
    {
        await _db.TaskCheckpoints.AddAsync(checkpoint, ct);
    }

    public async System.Threading.Tasks.Task<IEnumerable<TaskCheckpoint>> GetByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _db.TaskCheckpoints.AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.TargetPercent)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}

public sealed class AuditEventRepository : IAuditEventRepository
{
    private readonly AppDbContext _db;
    public AuditEventRepository(IDbContextProvider provider) => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task AddAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        await _db.AuditEvents.AddAsync(auditEvent, ct);
    }

    public async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
