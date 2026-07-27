using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using MangaERP.Shared.Domain.Entities;
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
        var chapterIds = await _db.Chapters
            .Where(c => c.SeriesId == seriesId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var tasksToRevoke = await _db.PageTasks
            .Where(t => t.AssignedAssistantId == assistantId &&
                        t.TaskStatus != PageTaskStatus.Approved &&
                        chapterIds.Contains(t.ChapterId))
            .ToListAsync(ct);

        DateTime now = DateTime.UtcNow;
        foreach (var task in tasksToRevoke)
        {
            task.MarkReassignmentRequired("Series access revoked by Mangaka.");

            var activeAttempts = await _db.TaskAssignmentAttempts
                .Where(a => a.TaskId == task.Id && a.AssistantId == assistantId &&
                            (a.Status == TaskAssignmentAttemptStatus.PendingAcceptance || a.Status == TaskAssignmentAttemptStatus.Accepted))
                .ToListAsync(ct);

            foreach (var attempt in activeAttempts)
            {
                attempt.Cancel(now, "Series access revoked by Mangaka.");
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task HandleCollaborationStateChangeAsync(
        Guid collaborationId,
        CollaborationStatus newStatus,
        CollaborationSuspensionMode? suspensionMode,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var collaboration = await _db.MangakaAssistantCollaborations.FirstOrDefaultAsync(c => c.Id == collaborationId, ct);
        if (collaboration == null) return;

        DateTime now = DateTime.UtcNow;

        // 1. Cancel all PendingAcceptance attempts for this collaboration
        var pendingAttempts = await _db.TaskAssignmentAttempts
            .Where(a => a.CollaborationId == collaborationId && a.Status == TaskAssignmentAttemptStatus.PendingAcceptance)
            .ToListAsync(ct);

        foreach (var attempt in pendingAttempts)
        {
            attempt.Cancel(now, $"Cancelled due to collaboration state change: {newStatus}");
            var task = await _db.PageTasks.FirstOrDefaultAsync(t => t.Id == attempt.TaskId, ct);
            if (task != null && task.TaskStatus == PageTaskStatus.PendingAcceptance)
            {
                task.MarkReassignmentRequired();
            }
        }

        // 2. If SuspendAllAccess or Ended: cancel accepted unfinished tasks as well
        if ((newStatus == CollaborationStatus.Suspended && suspensionMode == CollaborationSuspensionMode.SuspendAllAccess) ||
            newStatus == CollaborationStatus.Ended)
        {
            var acceptedAttempts = await _db.TaskAssignmentAttempts
                .Where(a => a.CollaborationId == collaborationId && a.Status == TaskAssignmentAttemptStatus.Accepted)
                .ToListAsync(ct);

            foreach (var attempt in acceptedAttempts)
            {
                var task = await _db.PageTasks.FirstOrDefaultAsync(t => t.Id == attempt.TaskId, ct);
                if (task != null && (task.TaskStatus == PageTaskStatus.Incomplete || task.TaskStatus == PageTaskStatus.RevisionAlert))
                {
                    attempt.Cancel(now, $"Cancelled due to collaboration state change: {newStatus}");
                    task.MarkReassignmentRequired();
                }
            }
        }

        // Audit the state change cascade
        _db.AuditEvents.Add(new AuditEvent(
            $"CollaborationTaskCascade_{newStatus}",
            actorUserId,
            "MangakaAssistantCollaboration",
            collaborationId,
            collaborationId,
            metadataJson: $"{{\"newStatus\":\"{newStatus}\",\"suspensionMode\":\"{suspensionMode}\"}}"));

        await _db.SaveChangesAsync(ct);
    }
}
