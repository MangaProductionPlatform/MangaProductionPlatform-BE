using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Chapter.Domain.Entities;
using Npgsql;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class StudioInvitationRepository : IStudioInvitationRepository
{
    private readonly AppDbContext _db;

    public StudioInvitationRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<StudioInvitation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.StudioInvitations.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async System.Threading.Tasks.Task<IEnumerable<StudioInvitation>> GetPendingByAssistantUserIdAsync(Guid assistantUserId, CancellationToken ct = default)
        => await _db.StudioInvitations
            .Where(i => i.AssistantUserId == assistantUserId && i.Status == StudioInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<StudioInvitation>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default)
        => await _db.StudioInvitations
            .Where(i => i.SeriesId == seriesId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public System.Threading.Tasks.Task<bool> HasPendingForMangakaEmailAsync(Guid mangakaId, string normalizedEmail, CancellationToken ct = default)
        => _db.StudioInvitations.AnyAsync(i => i.InviterMangakaId == mangakaId &&
            i.Status == StudioInvitationStatus.Pending && i.NormalizedAssistantEmail == normalizedEmail, ct);

    public async System.Threading.Tasks.Task<IEnumerable<StudioMemberInfo>> GetActiveMembersWithUsersBySeriesIdAsync(Guid seriesId, CancellationToken ct = default)
    {
        // Thành viên "active" = Accepted, hoặc IsNewAccountFlow + Pending (chưa kích hoạt nhưng đã được giao việc)
        var members = await _db.StudioInvitations
            .Where(i => i.SeriesId == seriesId
                && i.AssistantUserId != null
                && i.Status == StudioInvitationStatus.Accepted)
            .Join(_db.Users,
                inv => inv.AssistantUserId,
                user => user.Id,
                (inv, user) => new StudioMemberInfo(
                    inv.Id,
                    user.Id,
                    inv.AssistantEmail,
                    user.FullName,
                    user.AvatarUrl,
                    user.PenName,
                    inv.Status.ToString(),
                    // Thời điểm gia nhập: RespondedAt nếu TH2, CreatedAt nếu TH1 (new account)
                    inv.RespondedAt ?? inv.CreatedAt
                ))
            .OrderByDescending(m => m.JoinedAt)
            .ToListAsync(ct);

        return members;
    }

    public async System.Threading.Tasks.Task<StudioInvitation?> GetByActivationTokenAsync(string token, CancellationToken ct = default)
        => await _db.StudioInvitations.FirstOrDefaultAsync(i => i.ActivationToken == token, ct);

    public async System.Threading.Tasks.Task AddAsync(StudioInvitation invitation, CancellationToken ct = default)
        => await _db.StudioInvitations.AddAsync(invitation, ct);

    public System.Threading.Tasks.Task UpdateAsync(StudioInvitation invitation, CancellationToken ct = default)
    {
        _db.StudioInvitations.Update(invitation);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public System.Threading.Tasks.Task<bool> HasNonEndedCollaborationAsync(Guid assistantId, CancellationToken ct = default)
        => _db.MangakaAssistantCollaborations.AnyAsync(c => c.AssistantId == assistantId &&
            c.Status != CollaborationStatus.Ended, ct);

    public System.Threading.Tasks.Task<MangakaAssistantCollaboration?> GetCollaborationAsync(Guid id, CancellationToken ct = default)
        => _db.MangakaAssistantCollaborations.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async System.Threading.Tasks.Task<IEnumerable<MangakaAssistantCollaboration>> GetNonEndedCollaborationsByMangakaAsync(Guid mangakaId, CancellationToken ct = default)
        => await _db.MangakaAssistantCollaborations.Where(c => c.MangakaId == mangakaId &&
            c.Status != CollaborationStatus.Ended &&
            c.Status != CollaborationStatus.Rejected &&
            c.Status != CollaborationStatus.Cancelled).ToListAsync(ct);

    public System.Threading.Tasks.Task<MangakaAssistantCollaboration?> GetNonEndedCollaborationByAssistantAsync(Guid assistantId, CancellationToken ct = default)
        => _db.MangakaAssistantCollaborations.FirstOrDefaultAsync(c => c.AssistantId == assistantId && c.Status != CollaborationStatus.Ended, ct);

    public async System.Threading.Tasks.Task<MangakaAssistantCollaboration> AcceptInvitationAsync(
        Guid invitationId, Guid assistantId, Guid actorId, DateTime now, string? correlationId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var invitation = await _db.StudioInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct)
            ?? throw new KeyNotFoundException("Invitation was not found.");

        var actor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId, ct);
        if (actor is null || actor.IsDeleted || actor.AccountStatus != AccountStatus.Active ||
            actor.Role != UserRole.Assistant || actor.Id != assistantId)
            throw new UnauthorizedAccessException("Only the active Assistant account owning this invitation can accept it.");

        if (invitation.AssistantUserId != assistantId)
            throw new UnauthorizedAccessException("You cannot process this invitation.");

        if (invitation.Status != StudioInvitationStatus.Pending)
            throw new ConflictException("This invitation has already been processed.");

        if (invitation.ExpiresAt < now)
        {
            invitation.Status = StudioInvitationStatus.Expired;
            invitation.RespondedAt = now;
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw new ConflictException("This invitation has expired.");
        }

        if (await _db.MangakaAssistantCollaborations.AnyAsync(c => c.AssistantId == assistantId && c.Status != CollaborationStatus.Ended, ct))
            throw new ConflictException("The Assistant already has a non-ended Mangaka collaboration.");

        var collaboration = new MangakaAssistantCollaboration(invitation.InviterMangakaId, assistantId, invitation.Id, now);
        _db.MangakaAssistantCollaborations.Add(collaboration);
        if (invitation.SeriesId != Guid.Empty)
        {
            var grant = SeriesAccessGrant.Create(collaboration.Id, invitation.SeriesId, invitation.InviterMangakaId);
            _db.SeriesAccessGrants.Add(grant);
        }
        _db.CollaborationEvents.Add(new CollaborationEvent(
            collaboration.Id, CollaborationEventType.CollaborationActivated, actorId, now,
            correlationId: correlationId));
        invitation.Status = StudioInvitationStatus.Accepted;
        invitation.RespondedAt = now;
        try
        {
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return collaboration;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException("The invitation could not be accepted because it changed concurrently.");
        }
        catch (DbUpdateException ex) when (IsConcurrencyOrKnownUniqueViolation(ex))
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException("The invitation could not be accepted because the Assistant collaboration changed concurrently.");
        }
    }

    private static bool IsConcurrencyOrKnownUniqueViolation(DbUpdateException ex)
    {
        var postgres = FindPostgresException(ex);
        if (postgres is null) return false;
        if (postgres.SqlState is "40001" or "40P01") return true;
        if (postgres.SqlState != "23505") return false;
        var constraint = postgres.ConstraintName ?? string.Empty;
        return constraint.Contains("StudioInvitations", StringComparison.OrdinalIgnoreCase) ||
               constraint.Contains("MangakaAssistantCollaborations", StringComparison.OrdinalIgnoreCase) ||
               constraint.Contains("IX_StudioInvitations", StringComparison.OrdinalIgnoreCase) ||
               constraint.Contains("IX_MangakaAssistantCollaborations", StringComparison.OrdinalIgnoreCase);
    }

    private static PostgresException? FindPostgresException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
            if (current is PostgresException postgres) return postgres;
        return null;
    }

    public System.Threading.Tasks.Task AddCollaborationAsync(MangakaAssistantCollaboration collaboration, CancellationToken ct = default)
        => _db.MangakaAssistantCollaborations.AddAsync(collaboration, ct).AsTask();

    public System.Threading.Tasks.Task AddCollaborationEventAsync(CollaborationEvent collaborationEvent, CancellationToken ct = default)
        => _db.CollaborationEvents.AddAsync(collaborationEvent, ct).AsTask();

    public async System.Threading.Tasks.Task<Dictionary<Guid, AssistantWorkloadMetricsInfo>> GetAssistantWorkloadMetricsBatchAsync(IEnumerable<Guid> assistantIds, CancellationToken ct = default)
    {
        var idsList = assistantIds.Distinct().ToList();
        if (!idsList.Any()) return new Dictionary<Guid, AssistantWorkloadMetricsInfo>();

        DateTime now = DateTime.UtcNow;
        DateTime nearDeadlineThreshold = now.AddHours(24);

        var activeTasks = await _db.PageTasks.AsNoTracking()
            .Where(t => t.AssignedAssistantId != null &&
                        idsList.Contains(t.AssignedAssistantId.Value) &&
                        (t.TaskStatus == PageTaskStatus.Incomplete || t.TaskStatus == PageTaskStatus.RevisionAlert))
            .Select(t => new {
                AssistantId = t.AssignedAssistantId!.Value,
                t.Deadline
            })
            .ToListAsync(ct);

        return activeTasks
            .GroupBy(t => t.AssistantId)
            .ToDictionary(
                g => g.Key,
                g => new AssistantWorkloadMetricsInfo(
                    g.Count(),
                    g.Count(t => t.Deadline.HasValue && t.Deadline.Value < now),
                    g.Count(t => t.Deadline.HasValue && t.Deadline.Value >= now && t.Deadline.Value <= nearDeadlineThreshold)
                )
            );
    }

    public async System.Threading.Tasks.Task<IEnumerable<AssistantActiveTaskInfo>> GetAssistantActiveTasksAsync(Guid assistantId, CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime nearDeadlineThreshold = now.AddHours(24);

        var tasks = await _db.PageTasks.AsNoTracking()
            .Include(t => t.Chapter)
            .Where(t => t.AssignedAssistantId == assistantId &&
                        (t.TaskStatus == PageTaskStatus.Incomplete || t.TaskStatus == PageTaskStatus.RevisionAlert))
            .ToListAsync(ct);

        return tasks.Select(t => new AssistantActiveTaskInfo(
            t.Id,
            t.Chapter != null ? t.Chapter.SeriesId : Guid.Empty,
            t.ChapterId,
            t.PageNumber,
            t.TaskType.ToString(),
            t.TaskStatus.ToString(),
            t.ProgressPercent,
            t.WorkStartedAt,
            t.Deadline,
            t.Deadline.HasValue && t.Deadline.Value < now,
            t.Deadline.HasValue && t.Deadline.Value >= now && t.Deadline.Value <= nearDeadlineThreshold
        ));
    }

    public async System.Threading.Tasks.Task<IEnumerable<AssistantPendingExtensionInfo>> GetAssistantPendingExtensionRequestsAsync(Guid assistantId, CancellationToken ct = default)
    {
        var requests = await _db.DeadlineExtensionRequests.AsNoTracking()
            .Where(r => r.AssistantId == assistantId && r.Status == "Pending")
            .ToListAsync(ct);

        return requests.Select(r => new AssistantPendingExtensionInfo(
            r.Id,
            r.PageTaskId,
            r.RequestedDeadline,
            r.Reason,
            r.Status,
            r.CreatedAt
        ));
    }
}
