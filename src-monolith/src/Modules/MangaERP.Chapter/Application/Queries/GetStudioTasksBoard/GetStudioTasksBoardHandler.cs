using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MediatR;

namespace MangaERP.Chapter.Application.Queries.GetStudioTasksBoard;

public record GetStudioTasksBoardQuery(Guid SeriesId, Guid RequesterId, string RequesterRole) : IRequest<StudioTasksBoardDto>;

public record StudioTasksBoardDto(
    Guid SeriesId,
    List<BoardChapterDto> Chapters);

public record BoardChapterDto(
    Guid ChapterId,
    string ChapterTitle,
    decimal ChapterNumber,
    List<BoardTaskDto> Tasks);

public record BoardTaskDto(
    Guid TaskId,
    int PageNumber,
    string Status,
    string TaskType,
    string? Description,
    DateTime? Deadline,
    Guid? AssistantId,
    string? AssistantName,
    string? AssistantAvatarUrl);

public class GetStudioTasksBoardHandler : IRequestHandler<GetStudioTasksBoardQuery, StudioTasksBoardDto>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly ICollaborationAuthorizationService _collaborationAuth;

    public GetStudioTasksBoardHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        IUserRepository userRepo,
        ISeriesRepository seriesRepo,
        ICollaborationAuthorizationService collaborationAuth)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _userRepo = userRepo;
        _seriesRepo = seriesRepo;
        _collaborationAuth = collaborationAuth;
    }

    public async Task<StudioTasksBoardDto> Handle(GetStudioTasksBoardQuery query, CancellationToken ct)
    {
        var series = await _seriesRepo.GetByIdAsync(query.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {query.SeriesId} not found.");

        // Authorization check
        var isAuthorized = false;
        if (query.RequesterRole.Equals("Mangaka", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = (series.AuthorId == query.RequesterId);
        }
        else if (query.RequesterRole.Equals("TantouEditor", StringComparison.OrdinalIgnoreCase))
        {
            var mangaka = await _userRepo.GetByIdAsync(series.AuthorId, ct);
            isAuthorized = (mangaka != null && mangaka.ManagingTantouId == query.RequesterId);
        }
        else if (query.RequesterRole.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = await _collaborationAuth.HasActiveCollaborationAsync(
                series.AuthorId, query.RequesterId, ct) &&
                await _collaborationAuth.HasLegacySeriesScopeAsync(
                    series.AuthorId, series.Id, query.RequesterId, ct);
        }
        else if (query.RequesterRole.Equals("EditorInChief", StringComparison.OrdinalIgnoreCase) ||
                 query.RequesterRole.Equals("EditorialBoard", StringComparison.OrdinalIgnoreCase) ||
                 query.RequesterRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = true;
        }

        if (!isAuthorized)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this studio board.");
        }

        var chapters = await _chapterRepo.GetBySeriesIdAsync(query.SeriesId, ct);
        var boardChapters = new List<BoardChapterDto>();

        // Cache assistant profiles to avoid redundant DB hits
        var assistantCache = new Dictionary<Guid, (string Name, string? AvatarUrl)>();

        foreach (var chapter in chapters)
        {
            var tasks = await _pageTaskRepo.GetByChapterIdAsync(chapter.Id, ct);
            var boardTasks = new List<BoardTaskDto>();

            foreach (var task in tasks)
            {
                string? assistantName = null;
                string? assistantAvatar = null;

                if (task.AssignedAssistantId.HasValue)
                {
                    var assistantId = task.AssignedAssistantId.Value;
                    if (!assistantCache.TryGetValue(assistantId, out var profile))
                    {
                        var user = await _userRepo.GetByIdAsync(assistantId, ct);
                        if (user != null)
                        {
                            profile = (user.PenName ?? user.FullName ?? user.Username, user.AvatarUrl);
                            assistantCache[assistantId] = profile;
                        }
                        else
                        {
                            profile = ("Unknown Assistant", null);
                        }
                    }
                    assistantName = profile.Name;
                    assistantAvatar = profile.AvatarUrl;
                }

                boardTasks.Add(new BoardTaskDto(
                    task.Id,
                    task.PageNumber,
                    task.TaskStatus.ToString(),
                    task.TaskType.ToString(),
                    task.Description,
                    task.Deadline,
                    task.AssignedAssistantId,
                    assistantName,
                    assistantAvatar));
            }

            boardChapters.Add(new BoardChapterDto(
                chapter.Id,
                chapter.Title,
                chapter.ChapterNumber,
                boardTasks));
        }

        return new StudioTasksBoardDto(query.SeriesId, boardChapters);
    }
}
