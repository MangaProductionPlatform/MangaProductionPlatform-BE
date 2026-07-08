using MangaERP.Task.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetTaskComments;

public record GetTaskCommentsQuery(Guid PageTaskId) : IRequest<IEnumerable<TaskCommentDto>>;

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

    public GetTaskCommentsHandler(ITaskCommentRepository commentRepo)
    {
        _commentRepo = commentRepo;
    }

    public async Task<IEnumerable<TaskCommentDto>> Handle(GetTaskCommentsQuery query, CancellationToken ct)
    {
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
