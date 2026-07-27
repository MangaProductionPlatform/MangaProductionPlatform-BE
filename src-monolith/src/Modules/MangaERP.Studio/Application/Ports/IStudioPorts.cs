using MangaERP.Studio.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MangaERP.Studio.Application.Ports;

public interface IStudioInvitationRepository
{
    System.Threading.Tasks.Task<StudioInvitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<StudioInvitation>> GetPendingByAssistantUserIdAsync(Guid assistantUserId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<StudioInvitation>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> HasPendingForMangakaEmailAsync(Guid mangakaId, string normalizedEmail, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<StudioMemberInfo>> GetActiveMembersWithUsersBySeriesIdAsync(Guid seriesId, CancellationToken ct = default);
    System.Threading.Tasks.Task<StudioInvitation?> GetByActivationTokenAsync(string token, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(StudioInvitation invitation, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(StudioInvitation invitation, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> HasNonEndedCollaborationAsync(Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<MangakaAssistantCollaboration?> GetCollaborationAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<MangakaAssistantCollaboration?> GetNonEndedCollaborationByAssistantAsync(Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<MangakaAssistantCollaboration> AcceptInvitationAsync(Guid invitationId, Guid assistantId, Guid actorId, DateTime now, string? correlationId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<MangakaAssistantCollaboration>> GetNonEndedCollaborationsByMangakaAsync(Guid mangakaId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddCollaborationAsync(MangakaAssistantCollaboration collaboration, CancellationToken ct = default);
    System.Threading.Tasks.Task AddCollaborationEventAsync(CollaborationEvent collaborationEvent, CancellationToken ct = default);
    System.Threading.Tasks.Task<Dictionary<Guid, AssistantWorkloadMetricsInfo>> GetAssistantWorkloadMetricsBatchAsync(IEnumerable<Guid> assistantIds, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<AssistantActiveTaskInfo>> GetAssistantActiveTasksAsync(Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<AssistantPendingExtensionInfo>> GetAssistantPendingExtensionRequestsAsync(Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<UnassignedAssistantInfo>> GetUnassignedAssistantsAsync(Guid mangakaId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<AdminUnassignedAssistantInfo>> GetAdminUnassignedAssistantsAsync(CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> IsAssistantUnassignedAsync(Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<MangakaAssistantCollaboration> AdminAssignAssistantToMangakaAsync(Guid assistantId, Guid mangakaId, Guid adminUserId, string? reason, DateTime now, CancellationToken ct = default);
}

public record AdminUnassignedAssistantInfo(
    Guid AssistantId,
    string DisplayName,
    string Email,
    string AccountStatus,
    DateTime? LastCollaborationEndedAt,
    Guid? PreviousMangakaId,
    string? PreviousMangakaName,
    bool IsAssignable
);

public record UnassignedAssistantInfo(
    Guid UserId,
    string Username,
    string FullName,
    string PersonalEmail,
    string? PhoneNumber,
    DateTime CreatedAt
);

public record AssistantWorkloadMetricsInfo(
    int ActiveTaskCount,
    int OverdueTaskCount,
    int NearDeadlineTaskCount
);

public record AssistantActiveTaskInfo(
    Guid TaskId,
    Guid SeriesId,
    Guid ChapterId,
    int PageNumber,
    string TaskType,
    string TaskStatus,
    int ProgressPercent,
    DateTime? WorkStartedAt,
    DateTime? Deadline,
    bool IsOverdue,
    bool IsNearDeadline
);

public record AssistantPendingExtensionInfo(
    Guid RequestId,
    Guid TaskId,
    DateTime RequestedDeadline,
    string Reason,
    string Status,
    DateTime CreatedAt
);

public interface ICollaborationAuthorizationService
{
    System.Threading.Tasks.Task<bool> HasActiveCollaborationAsync(Guid mangakaId, Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> HasLegacySeriesScopeAsync(Guid mangakaId, Guid seriesId, Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanReceiveNewAssignmentsAsync(Guid mangakaId, Guid seriesId, Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanAccessSeriesAsync(Guid seriesId, Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanBrowseSeriesAsync(Guid assistantId, Guid seriesId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanAccessTaskAsync(Guid assistantId, Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanAccessTaskResourcesAsync(Guid assistantId, Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanReceiveAssignmentAsync(Guid assistantId, Guid seriesId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanRespondToAssignmentAsync(Guid assistantId, Guid attemptId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanSubmitProgressAsync(Guid assistantId, Guid taskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> CanCompleteTaskAsync(Guid assistantId, Guid taskId, CancellationToken ct = default);
}

public interface ISeriesAccessGrantRepository
{
    System.Threading.Tasks.Task<SeriesAccessGrant?> GetActiveGrantAsync(Guid collaborationId, Guid seriesId, CancellationToken ct = default);
    System.Threading.Tasks.Task<SeriesAccessGrant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<SeriesAccessGrant>> GetByCollaborationIdAsync(Guid collaborationId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(SeriesAccessGrant grant, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(SeriesAccessGrant grant, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IStudioTaskRevocationService
{
    System.Threading.Tasks.Task RevokeActiveTasksForRemovedMemberAsync(
        Guid seriesId,
        Guid assistantId,
        CancellationToken ct = default);

    System.Threading.Tasks.Task HandleCollaborationStateChangeAsync(
        Guid collaborationId,
        CollaborationStatus newStatus,
        CollaborationSuspensionMode? suspensionMode,
        Guid actorUserId,
        CancellationToken ct = default);
}

public record StudioMemberInfo(
    Guid InvitationId,
    Guid AssistantUserId,
    string AssistantEmail,
    string? FullName,
    string? AvatarUrl,
    string? PenName,
    string InvitationStatus,
    DateTime JoinedAt
);

public interface IStudioIdentityService
{
    System.Threading.Tasks.Task<Guid?> FindActiveAssistantByEmailAsync(string email, CancellationToken ct = default);
    System.Threading.Tasks.Task<bool> IsInternalEmailAsync(string email, CancellationToken ct = default);

    System.Threading.Tasks.Task<(Guid userId, string activationToken)> ProvisionAssistantAccountAsync(
        string email, string? fullName, string invitingMangakaName, CancellationToken ct = default);
    System.Threading.Tasks.Task SendAssistantRegistrationEmailAsync(
        Guid userId, string activationToken, CancellationToken ct = default);

    System.Threading.Tasks.Task SendStudioInvitationNotificationAsync(
        Guid receiverUserId, Guid invitationId, string mangakaName, string seriesTitle, CancellationToken ct = default);
    System.Threading.Tasks.Task DeliverStudioInvitationRealtimeAsync(
        Guid receiverUserId, Guid invitationId, string seriesTitle, CancellationToken ct = default);
}
