using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Queries.RecommendAssistants;

public record RecommendAssistantsQuery(Guid ChapterId, Guid RequesterId) : IRequest<IEnumerable<RecommendedAssistantDto>>;

public record RecommendedAssistantDto(
    Guid AssistantId,
    string AssistantName,
    string? AvatarUrl,
    string? PenName,
    int ActiveTasksCount);

public class RecommendAssistantsHandler : IRequestHandler<RecommendAssistantsQuery, IEnumerable<RecommendedAssistantDto>>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IStudioInvitationRepository _studioInvitationRepo;
    private readonly IUserRepository _userRepo;

    public RecommendAssistantsHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        IStudioInvitationRepository studioInvitationRepo,
        IUserRepository userRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _studioInvitationRepo = studioInvitationRepo;
        _userRepo = userRepo;
    }

    public async Task<IEnumerable<RecommendedAssistantDto>> Handle(RecommendAssistantsQuery query, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(query.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {query.ChapterId} not found.");

        // Fetch all accepted studio members for this series
        var invitations = await _studioInvitationRepo.GetBySeriesIdAsync(chapter.SeriesId, ct);
        var activeAssistants = invitations
            .Where(i => i.Status == StudioInvitationStatus.Accepted && i.AssistantUserId.HasValue)
            .Select(i => i.AssistantUserId!.Value)
            .Distinct()
            .ToList();

        var recommendations = new List<RecommendedAssistantDto>();

        foreach (var assistantId in activeAssistants)
        {
            var user = await _userRepo.GetByIdAsync(assistantId, ct);
            if (user == null) continue;

            // Fetch tasks for this assistant to calculate active workload
            var tasks = await _pageTaskRepo.GetByAssistantAsync(assistantId, ct);
            var activeWorkloadCount = tasks.Count(t => t.TaskStatus != PageTaskStatus.Approved);

            recommendations.Add(new RecommendedAssistantDto(
                assistantId,
                user.FullName ?? user.PenName ?? user.Username,
                user.AvatarUrl,
                user.PenName,
                activeWorkloadCount));
        }

        // Sort by active tasks count ascending (least busy assistant first)
        return recommendations.OrderBy(r => r.ActiveTasksCount);
    }
}
