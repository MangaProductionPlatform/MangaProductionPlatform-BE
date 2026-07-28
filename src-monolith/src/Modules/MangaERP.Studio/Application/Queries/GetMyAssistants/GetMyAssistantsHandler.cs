using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Studio.Application.Queries.GetMyAssistants;

public record GetMyAssistantsQuery(Guid MangakaId) : IRequest<MyAssistantsResponseDto>;

public record MySeriesAccessDto(
    Guid SeriesId,
    bool IsActive
);

public record MyAssistantDto(
    Guid AssistantId,
    string DisplayName,
    string Email,
    Guid CollaborationId,
    string CollaborationStatus,
    string AccountStatus,
    int ActiveTaskCount,
    int MaxWorkload,
    int RemainingCapacity,
    int OverdueTaskCount,
    int NearDeadlineTaskCount,
    DateTime AssignedAt,
    Guid ConcurrencyToken,
    Guid ExpectedConcurrencyToken,
    List<MySeriesAccessDto> SeriesAccess
);

public record MyAssistantsResponseDto(
    List<MyAssistantDto> Assistants
);

public class GetMyAssistantsHandler : IRequestHandler<GetMyAssistantsQuery, MyAssistantsResponseDto>
{
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly IUserRepository _userRepo;
    private readonly int _defaultMaxWorkload;

    public GetMyAssistantsHandler(
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

    public async Task<MyAssistantsResponseDto> Handle(GetMyAssistantsQuery request, CancellationToken ct)
    {
        var collaborations = (await _collabRepo.GetNonEndedCollaborationsByMangakaAsync(request.MangakaId, ct)).ToList();
        if (!collaborations.Any())
        {
            return new MyAssistantsResponseDto(new List<MyAssistantDto>());
        }

        var assistantIds = collaborations.Select(c => c.AssistantId).Distinct().ToList();
        var workloadMetricsMap = await _collabRepo.GetAssistantWorkloadMetricsBatchAsync(assistantIds, ct);

        var resultList = new List<MyAssistantDto>();

        foreach (var collab in collaborations)
        {
            var assistant = await _userRepo.GetByIdAsync(collab.AssistantId, ct);
            if (assistant == null || assistant.IsDeleted) continue;

            var grants = await _grantRepo.GetByCollaborationIdAsync(collab.Id, ct);
            var seriesAccessList = grants.Select(g => new MySeriesAccessDto(g.SeriesId, g.IsActive)).ToList();

            string displayName = !string.IsNullOrWhiteSpace(assistant.FullName)
                ? assistant.FullName
                : assistant.Username;

            int activeTaskCount = 0;
            int overdueTaskCount = 0;
            int nearDeadlineTaskCount = 0;

            if (workloadMetricsMap.TryGetValue(assistant.Id, out var metrics))
            {
                activeTaskCount = metrics.ActiveTaskCount;
                overdueTaskCount = metrics.OverdueTaskCount;
                nearDeadlineTaskCount = metrics.NearDeadlineTaskCount;
            }

            int maxWorkload = _defaultMaxWorkload;
            int remainingCapacity = Math.Max(0, maxWorkload - activeTaskCount);

            resultList.Add(new MyAssistantDto(
                assistant.Id,
                displayName,
                assistant.PersonalEmail ?? assistant.Email,
                collab.Id,
                collab.Status.ToString(),
                assistant.AccountStatus.ToString(),
                activeTaskCount,
                maxWorkload,
                remainingCapacity,
                overdueTaskCount,
                nearDeadlineTaskCount,
                collab.StartedAt != default ? collab.StartedAt : collab.CreatedAt,
                collab.ConcurrencyToken,
                collab.ConcurrencyToken,
                seriesAccessList));
        }

        return new MyAssistantsResponseDto(resultList);
    }
}
