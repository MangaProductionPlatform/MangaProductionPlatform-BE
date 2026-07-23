using MangaERP.Task.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetTaskComments;

public record GetTaskCommentsQuery(Guid PageTaskId, Guid RequesterId, string RequesterRole) : IRequest<IEnumerable<TaskCommentDto>>;

public record TaskCommentDto(
    Guid CommentId,
    Guid PageTaskId,
    Guid UserId,
    string UserFullName,
    string Content,
    DateTime CreatedAt);

public class GetTaskCommentsHandler : IRequestHandler<GetTaskCommentsQuery, IEnumerable<TaskCommentDto>>
{
    private readonly ITaskCommentRepository _commentRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly ICollaborationAuthorizationService _collaborationAuth;
    private readonly IUserRepository _userRepo;

    public GetTaskCommentsHandler(
        ITaskCommentRepository commentRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        ICollaborationAuthorizationService collaborationAuth,
        IUserRepository userRepo)
    {
        _commentRepo = commentRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _collaborationAuth = collaborationAuth;
        _userRepo = userRepo;
    }

    public async Task<IEnumerable<TaskCommentDto>> Handle(GetTaskCommentsQuery query, CancellationToken ct)
    {
        var task = await _pageTaskRepo.GetByIdAsync(query.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Task {query.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {task.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

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
            var isAssigned = (task.AssignedAssistantId == query.RequesterId);
            var isMember = await _collaborationAuth.HasActiveCollaborationAsync(series.AuthorId, query.RequesterId, ct) &&
                           await _collaborationAuth.HasLegacySeriesScopeAsync(series.AuthorId, series.Id, query.RequesterId, ct);
            isAuthorized = isAssigned || isMember;
        }
        else if (query.RequesterRole.Equals("EditorInChief", StringComparison.OrdinalIgnoreCase) ||
                 query.RequesterRole.Equals("EditorialBoard", StringComparison.OrdinalIgnoreCase) ||
                 query.RequesterRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = true;
        }

        if (!isAuthorized)
        {
            throw new UnauthorizedAccessException("You are not authorized to view comments on this task.");
        }

        var comments = await _commentRepo.GetByPageTaskIdAsync(query.PageTaskId, ct);
        return comments.Select(c => new TaskCommentDto(
            c.Id,
            c.PageTaskId,
            c.UserId,
            c.UserFullName,
            c.Content,
            c.CreatedAt));
    }
}
