using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Studio.Application.Queries.GetMyAssistants;
using MangaERP.Shared.Domain.Exceptions;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Studio.Application.Queries.GetAssistantDetail;

public record GetAssistantDetailQuery(Guid MangakaId, Guid AssistantId) : IRequest<AssistantDetailResponseDto>;

public record AssistantActiveTaskDto(
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

public record ExtensionRequestItemDto(
    Guid RequestId,
    Guid TaskId,
    DateTime RequestedDeadline,
    string Reason,
    string Status,
    DateTime RequestedAt
);

public record AssistantDetailResponseDto(
    Guid AssistantId,
    string DisplayName,
    string Email,
    string AccountStatus,
    Guid CollaborationId,
    string CollaborationStatus,
    DateTime AssignedAt,
    int ActiveTaskCount,
    int MaxWorkload,
    int RemainingCapacity,
    int OverdueTaskCount,
    int NearDeadlineTaskCount,
    List<MySeriesAccessDto> SeriesAccess,
    List<AssistantActiveTaskDto> ActiveTasks,
    List<ExtensionRequestItemDto> PendingExtensionRequests
);

public class GetAssistantDetailHandler : IRequestHandler<GetAssistantDetailQuery, AssistantDetailResponseDto>
{
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly IUserRepository _userRepo;
    private readonly int _defaultMaxWorkload;

    public GetAssistantDetailHandler(
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        IUserRepository userRepo,
        IConfiguration config)
    {
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
        _userRepo = userRepo;
        _defaultMaxWorkload = config.GetValue<int?>("AssistantWorkload:MaximumActiveAssignments") ?? 3;
    }

    public async Task<AssistantDetailResponseDto> Handle(GetAssistantDetailQuery request, CancellationToken ct)
    {
        var collab = await _collabRepo.GetNonEndedCollaborationByAssistantAsync(request.AssistantId, ct);
        if (collab == null || collab.MangakaId != request.MangakaId ||
            collab.Status == CollaborationStatus.Ended ||
            collab.Status == CollaborationStatus.Rejected ||
            collab.Status == CollaborationStatus.Cancelled)
        {
            throw new UnauthorizedAccessException("Assistant is not within your management scope.");
        }

        var assistant = await _userRepo.GetByIdAsync(request.AssistantId, ct)
            ?? throw new EntityNotFoundException("User", request.AssistantId);

        if (assistant.IsDeleted)
            throw new EntityNotFoundException("User", request.AssistantId);

        var grants = await _grantRepo.GetByCollaborationIdAsync(collab.Id, ct);
        var seriesAccessList = grants.Select(g => new MySeriesAccessDto(g.SeriesId, g.IsActive)).ToList();

        var activeTaskInfos = await _collabRepo.GetAssistantActiveTasksAsync(request.AssistantId, ct);
        var activeTaskDtos = activeTaskInfos.Select(t => new AssistantActiveTaskDto(
            t.TaskId,
            t.SeriesId,
            t.ChapterId,
            t.PageNumber,
            t.TaskType,
            t.TaskStatus,
            t.ProgressPercent,
            t.WorkStartedAt,
            t.Deadline,
            t.IsOverdue,
            t.IsNearDeadline
        )).ToList();

        var extensionInfos = await _collabRepo.GetAssistantPendingExtensionRequestsAsync(request.AssistantId, ct);
        var extensionDtos = extensionInfos.Select(r => new ExtensionRequestItemDto(
            r.RequestId,
            r.TaskId,
            r.RequestedDeadline,
            r.Reason,
            r.Status,
            r.CreatedAt
        )).ToList();

        int activeTaskCount = activeTaskDtos.Count;
        int maxWorkload = _defaultMaxWorkload;
        int remainingCapacity = Math.Max(0, maxWorkload - activeTaskCount);
        int overdueTaskCount = activeTaskDtos.Count(t => t.IsOverdue);
        int nearDeadlineTaskCount = activeTaskDtos.Count(t => t.IsNearDeadline);

        string displayName = !string.IsNullOrWhiteSpace(assistant.FullName)
            ? assistant.FullName
            : assistant.Username;

        return new AssistantDetailResponseDto(
            assistant.Id,
            displayName,
            assistant.Email,
            assistant.AccountStatus.ToString(),
            collab.Id,
            collab.Status.ToString(),
            collab.StartedAt != default ? collab.StartedAt : collab.CreatedAt,
            activeTaskCount,
            maxWorkload,
            remainingCapacity,
            overdueTaskCount,
            nearDeadlineTaskCount,
            seriesAccessList,
            activeTaskDtos,
            extensionDtos);
    }
}
