using MangaERP.Studio.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;

namespace MangaERP.Shared.Infrastructure.Services;

public class StudioTaskRevocationService : IStudioTaskRevocationService
{
    private readonly AppDbContext _db;

    public StudioTaskRevocationService(IDbContextProvider provider)
    {
        _db = (AppDbContext)provider.GetDbContext();
    }

    public async System.Threading.Tasks.Task RevokeActiveTasksForRemovedMemberAsync(
        Guid seriesId,
        Guid assistantId,
        CancellationToken ct = default)
    {
        var tasksToRevoke = await _db.PageTasks
            .Include(t => t.Chapter)
            .Where(t => t.AssignedAssistantId == assistantId &&
                        t.TaskStatus != PageTaskStatus.Approved &&
                        t.Chapter.SeriesId == seriesId)
            .ToListAsync(ct);

        foreach (var task in tasksToRevoke)
        {
            task.Revoke();
        }
    }
}
