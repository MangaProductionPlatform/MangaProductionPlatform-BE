using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Series.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MangaERP.Task.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Task.Application.Queries.GetAssistantCandidates;

public record GetAssistantCandidatesQuery(Guid TaskId, Guid ActorUserId)
    : IRequest<TaskAssistantCandidatesResultDto>;

public record GetChapterAssistantCandidatesQuery(Guid ChapterId, Guid ActorUserId)
    : IRequest<ChapterAssistantCandidatesResultDto>;

public record AssistantCandidateDto(
    Guid AssistantId,
    string DisplayName,
    string? Email,
    int ActiveTaskCount,
    int PendingAssignmentCount,
    int TotalWorkload,
    int MaxWorkload,
    int RemainingCapacity,
    bool HasSeriesAccess,
    bool IsAvailable,
    string AvailabilityCode,
    string? AvailabilityReason);

public record TaskAssistantCandidatesResultDto(
    Guid TaskId,
    Guid SeriesId,
    int MaxWorkload,
    List<AssistantCandidateDto> AvailableAssistants,
    List<AssistantCandidateDto> UnavailableAssistants);

public record ChapterAssistantCandidatesResultDto(
    Guid ChapterId,
    Guid SeriesId,
    int MaxWorkload,
    List<AssistantCandidateDto> AvailableAssistants,
    List<AssistantCandidateDto> UnavailableAssistants);

public static class AssistantCandidateHelper
{
    public static async Task<(List<AssistantCandidateDto> Available, List<AssistantCandidateDto> Unavailable, int MaxWorkload)> GetCandidatesAsync(
        Guid mangakaId,
        Guid seriesId,
        PageTask? task,
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        IUserRepository userRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        IConfiguration config,
        CancellationToken ct)
    {
        int maxWorkload = config.GetValue<int?>("AssistantWorkload:MaximumActiveAssignments") ?? 3;

        var collaborations = (await collabRepo.GetNonEndedCollaborationsByMangakaAsync(mangakaId, ct)).ToList();
        var existingAttemptsOnTask = task != null ? (await attemptRepo.GetByTaskIdAsync(task.Id, ct)).ToList() : null;

        var availableList = new List<AssistantCandidateDto>();
        var unavailableList = new List<AssistantCandidateDto>();

        foreach (var collab in collaborations)
        {
            var assistant = await userRepo.GetByIdAsync(collab.AssistantId, ct);
            if (assistant == null) continue;

            bool isAccountActive = assistant.AccountStatus == AccountStatus.Active && !assistant.IsDeleted;
            bool isCollabActive = collab.Status == CollaborationStatus.Accepted;

            var grant = await grantRepo.GetActiveGrantAsync(collab.Id, seriesId, ct);
            bool hasSeriesAccess = grant != null;

            int pendingCount = 0;
            int totalWorkload = await attemptRepo.GetActiveWorkloadCountAsync(assistant.Id, ct);
            int activeTaskCount = totalWorkload;
            int remainingCapacity = Math.Max(0, maxWorkload - totalWorkload);

            bool isAssignedToThisTask = task != null && (task.AssignedAssistantId == assistant.Id || (existingAttemptsOnTask != null && existingAttemptsOnTask.Any(a => a.AssistantId == assistant.Id && a.Status == TaskAssignmentAttemptStatus.Accepted)));
            bool isExcludedPreviousAssistant = task != null && task.ShouldExcludePreviousAssistant(assistant.Id);

            AssistantAvailabilityCode code;
            string? reason;

            if (!isAccountActive)
            {
                code = AssistantAvailabilityCode.AccountInactive;
                reason = "Assistant account is inactive or deleted.";
            }
            else if (!isCollabActive)
            {
                code = AssistantAvailabilityCode.CollaborationInactive;
                reason = $"Collaboration status is '{collab.Status}'. Only Active collaborations can be assigned.";
            }
            else if (!hasSeriesAccess)
            {
                code = AssistantAvailabilityCode.SeriesAccessMissing;
                reason = "Assistant has not been granted access to this series.";
            }
            else if (task != null && isExcludedPreviousAssistant)
            {
                code = AssistantAvailabilityCode.PreviousTaskAssigneeExcluded;
                reason = "This assistant was removed from the previous version of this task.";
            }
            else if (totalWorkload >= maxWorkload)
            {
                code = AssistantAvailabilityCode.WorkloadLimitReached;
                reason = $"Assistant has reached the maximum workload limit ({maxWorkload}).";
            }
            else if (task != null && isAssignedToThisTask)
            {
                code = AssistantAvailabilityCode.AlreadyAssignedToTask;
                reason = "Assistant is already assigned to this task.";
            }
            else
            {
                code = AssistantAvailabilityCode.Available;
                reason = null;
            }

            bool isAvailable = code == AssistantAvailabilityCode.Available;

            string displayName = !string.IsNullOrWhiteSpace(assistant.FullName)
                ? assistant.FullName
                : assistant.Username;

            var dto = new AssistantCandidateDto(
                assistant.Id,
                displayName,
                assistant.Email,
                activeTaskCount,
                pendingCount,
                totalWorkload,
                maxWorkload,
                remainingCapacity,
                hasSeriesAccess,
                isAvailable,
                code.ToString(),
                reason);

            if (isAvailable)
                availableList.Add(dto);
            else
                unavailableList.Add(dto);
        }

        availableList = availableList
            .OrderBy(a => a.ActiveTaskCount)
            .ThenBy(a => a.DisplayName)
            .ToList();

        unavailableList = unavailableList
            .OrderBy(a => a.AvailabilityCode)
            .ThenBy(a => a.ActiveTaskCount)
            .ThenBy(a => a.DisplayName)
            .ToList();

        return (availableList, unavailableList, maxWorkload);
    }
}

public class GetAssistantCandidatesHandler : IRequestHandler<GetAssistantCandidatesQuery, TaskAssistantCandidatesResultDto>
{
    private readonly IPageTaskRepository _taskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly IConfiguration _config;

    public GetAssistantCandidatesHandler(
        IPageTaskRepository taskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        IUserRepository userRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        IConfiguration config)
    {
        _taskRepo = taskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
        _userRepo = userRepo;
        _attemptRepo = attemptRepo;
        _config = config;
    }

    public async Task<TaskAssistantCandidatesResultDto> Handle(GetAssistantCandidatesQuery request, CancellationToken ct)
    {
        var task = await _taskRepo.GetByIdAsync(request.TaskId, ct)
            ?? throw new EntityNotFoundException("PageTask", request.TaskId);

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new EntityNotFoundException("Chapter", task.ChapterId);

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new EntityNotFoundException("MangaSeries", chapter.SeriesId);

        if (series.AuthorId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the Mangaka who owns the series can view candidate assistants.");

        var (available, unavailable, maxWorkload) = await AssistantCandidateHelper.GetCandidatesAsync(
            series.AuthorId, series.Id, task, _collabRepo, _grantRepo, _userRepo, _attemptRepo, _config, ct);

        return new TaskAssistantCandidatesResultDto(
            task.Id,
            series.Id,
            maxWorkload,
            available,
            unavailable);
    }
}

public class GetChapterAssistantCandidatesHandler : IRequestHandler<GetChapterAssistantCandidatesQuery, ChapterAssistantCandidatesResultDto>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly IConfiguration _config;

    public GetChapterAssistantCandidatesHandler(
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        IUserRepository userRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        IConfiguration config)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
        _userRepo = userRepo;
        _attemptRepo = attemptRepo;
        _config = config;
    }

    public async Task<ChapterAssistantCandidatesResultDto> Handle(GetChapterAssistantCandidatesQuery request, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, ct)
            ?? throw new EntityNotFoundException("Chapter", request.ChapterId);

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new EntityNotFoundException("MangaSeries", chapter.SeriesId);

        if (series.AuthorId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the Mangaka who owns the series can view candidate assistants.");

        var (available, unavailable, maxWorkload) = await AssistantCandidateHelper.GetCandidatesAsync(
            series.AuthorId, series.Id, null, _collabRepo, _grantRepo, _userRepo, _attemptRepo, _config, ct);

        return new ChapterAssistantCandidatesResultDto(
            chapter.Id,
            series.Id,
            maxWorkload,
            available,
            unavailable);
    }
}
