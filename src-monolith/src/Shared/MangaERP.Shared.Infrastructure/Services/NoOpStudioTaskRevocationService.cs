using MangaERP.Studio.Application.Ports;

namespace MangaERP.Shared.Infrastructure.Services;

public class NoOpStudioTaskRevocationService : IStudioTaskRevocationService
{
    public System.Threading.Tasks.Task RevokeActiveTasksForRemovedMemberAsync(
        Guid seriesId,
        Guid assistantId,
        CancellationToken ct = default)
    {
        // TODO(Nam): Replace this with task-side revocation for non-approved PageTasks
        // assigned to the removed assistant in the target series before SaveChangesAsync.
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
