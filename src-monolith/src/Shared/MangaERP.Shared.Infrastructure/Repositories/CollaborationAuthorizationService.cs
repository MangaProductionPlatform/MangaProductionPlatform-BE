using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace MangaERP.Shared.Infrastructure.Repositories;

/// <summary>
/// Single policy boundary for collaboration-aware authorization.
/// Enforces Active, SuspendNewAssignments, SuspendAllAccess, EndingRequested, and Ended collaboration states.
/// </summary>
public sealed class CollaborationAuthorizationService : ICollaborationAuthorizationService
{
    private readonly AppDbContext _db;

    public CollaborationAuthorizationService(AppDbContext db) => _db = db;

    public Task<bool> HasActiveCollaborationAsync(Guid mangakaId, Guid assistantId, CancellationToken ct = default) =>
        _db.MangakaAssistantCollaborations.AsNoTracking().AnyAsync(
            c => c.MangakaId == mangakaId && c.AssistantId == assistantId &&
                 c.Status == CollaborationStatus.Active, ct);

    public Task<bool> HasLegacySeriesScopeAsync(Guid mangakaId, Guid seriesId, Guid assistantId, CancellationToken ct = default) =>
        _db.StudioInvitations.AsNoTracking().AnyAsync(i =>
            i.InviterMangakaId == mangakaId && i.SeriesId == seriesId &&
            i.AssistantUserId == assistantId && i.Status == StudioInvitationStatus.Accepted, ct);

    public async Task<bool> CanReceiveNewAssignmentsAsync(Guid mangakaId, Guid seriesId, Guid assistantId, CancellationToken ct = default)
    {
        var collaboration = await _db.MangakaAssistantCollaborations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.MangakaId == mangakaId && c.AssistantId == assistantId && c.Status == CollaborationStatus.Active, ct);

        if (collaboration == null) return false;

        return await _db.SeriesAccessGrants.AsNoTracking()
            .AnyAsync(g => g.CollaborationId == collaboration.Id && g.SeriesId == seriesId && g.RevokedAt == null, ct);
    }

    public async Task<bool> CanAccessSeriesAsync(Guid seriesId, Guid assistantId, CancellationToken ct = default)
    {
        return await _db.SeriesAccessGrants.AsNoTracking()
            .AnyAsync(g => g.SeriesId == seriesId && g.RevokedAt == null &&
                           _db.MangakaAssistantCollaborations.Any(c => c.Id == g.CollaborationId && c.AssistantId == assistantId && c.Status == CollaborationStatus.Active), ct);
    }

    public async Task<bool> CanBrowseSeriesAsync(Guid assistantId, Guid seriesId, CancellationToken ct = default)
    {
        return await CanAccessSeriesAsync(seriesId, assistantId, ct);
    }

    public async Task<bool> CanAccessTaskAsync(Guid assistantId, Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.PageTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null) return false;

        var chapter = await _db.Chapters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == task.ChapterId, ct);
        if (chapter == null) return false;

        var series = await _db.MangaSeries.AsNoTracking().FirstOrDefaultAsync(s => s.Id == chapter.SeriesId, ct);
        if (series != null && series.AuthorId == assistantId)
            return true;

        if (task.AssignedAssistantId != null && task.AssignedAssistantId != assistantId)
            return false;

        var collab = await _db.MangakaAssistantCollaborations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AssistantId == assistantId && c.Status != CollaborationStatus.Ended, ct);

        if (collab == null) return false;

        if (collab.Status == CollaborationStatus.Active)
        {
            return await _db.SeriesAccessGrants.AsNoTracking()
                .AnyAsync(g => g.CollaborationId == collab.Id && g.SeriesId == chapter.SeriesId && g.RevokedAt == null, ct);
        }

        if (collab.Status == CollaborationStatus.Suspended && collab.SuspensionMode == CollaborationSuspensionMode.SuspendAllAccess)
        {
            return false;
        }

        return await _db.TaskAssignmentAttempts.AsNoTracking()
            .AnyAsync(a => a.TaskId == taskId && a.AssistantId == assistantId && a.Status == TaskAssignmentAttemptStatus.Accepted, ct);
    }

    public async Task<bool> CanAccessTaskResourcesAsync(Guid assistantId, Guid taskId, CancellationToken ct = default)
    {
        return await CanAccessTaskAsync(assistantId, taskId, ct);
    }

    public async Task<bool> CanReceiveAssignmentAsync(Guid assistantId, Guid seriesId, CancellationToken ct = default)
    {
        var collab = await _db.MangakaAssistantCollaborations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AssistantId == assistantId && c.Status == CollaborationStatus.Active, ct);

        if (collab == null) return false;

        return await _db.SeriesAccessGrants.AsNoTracking()
            .AnyAsync(g => g.CollaborationId == collab.Id && g.SeriesId == seriesId && g.RevokedAt == null, ct);
    }

    public async Task<bool> CanRespondToAssignmentAsync(Guid assistantId, Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await _db.TaskAssignmentAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == attemptId, ct);
        if (attempt == null || attempt.AssistantId != assistantId || attempt.Status != TaskAssignmentAttemptStatus.PendingAcceptance)
            return false;

        var collab = await _db.MangakaAssistantCollaborations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == attempt.CollaborationId, ct);

        return collab != null && collab.Status == CollaborationStatus.Active;
    }

    public async Task<bool> CanSubmitProgressAsync(Guid assistantId, Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.PageTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null || (task.TaskStatus != PageTaskStatus.Incomplete && task.TaskStatus != PageTaskStatus.RevisionAlert))
            return false;

        if (task.AssignedAssistantId != assistantId)
            return false;

        var collab = await _db.MangakaAssistantCollaborations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AssistantId == assistantId && c.Status != CollaborationStatus.Ended, ct);

        if (collab == null || (collab.Status == CollaborationStatus.Suspended && collab.SuspensionMode == CollaborationSuspensionMode.SuspendAllAccess))
            return false;

        return await _db.TaskAssignmentAttempts.AsNoTracking()
            .AnyAsync(a => a.TaskId == taskId && a.AssistantId == assistantId && a.Status == TaskAssignmentAttemptStatus.Accepted, ct);
    }

    public async Task<bool> CanCompleteTaskAsync(Guid assistantId, Guid taskId, CancellationToken ct = default)
    {
        return await CanSubmitProgressAsync(assistantId, taskId, ct);
    }
}
