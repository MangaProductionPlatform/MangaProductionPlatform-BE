using FluentValidation;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Application.Queries.GetTaskComments;
using MediatR;

namespace MangaERP.Task.Application.Commands.AddComment;

public record AddCommentCommand(
    Guid PageTaskId,
    Guid UserId,
    string UserRole,
    string Content
) : IRequest<TaskCommentDto>;

public class AddCommentHandler : IRequestHandler<AddCommentCommand, TaskCommentDto>
{
    private readonly ITaskCommentRepository _commentRepo;
    private readonly IUserRepository _userRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IStudioInvitationRepository _studioInvitationRepo;

    public AddCommentHandler(
        ITaskCommentRepository commentRepo,
        IUserRepository userRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IStudioInvitationRepository studioInvitationRepo)
    {
        _commentRepo = commentRepo;
        _userRepo = userRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _studioInvitationRepo = studioInvitationRepo;
    }

    public async Task<TaskCommentDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var task = await _pageTaskRepo.GetByIdAsync(cmd.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Task {cmd.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {task.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        var isAuthorized = false;
        if (cmd.UserRole.Equals("Mangaka", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = (series.AuthorId == cmd.UserId);
        }
        else if (cmd.UserRole.Equals("TantouEditor", StringComparison.OrdinalIgnoreCase))
        {
            var mangaka = await _userRepo.GetByIdAsync(series.AuthorId, ct);
            isAuthorized = (mangaka != null && mangaka.ManagingTantouId == cmd.UserId);
        }
        else if (cmd.UserRole.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
        {
            var isAssigned = (task.AssignedAssistantId == cmd.UserId);
            var invitations = await _studioInvitationRepo.GetBySeriesIdAsync(series.Id, ct);
            var isMember = invitations.Any(i => i.AssistantUserId == cmd.UserId &&
                                                i.Status == StudioInvitationStatus.Accepted);
            isAuthorized = isAssigned || isMember;
        }
        else if (cmd.UserRole.Equals("EditorInChief", StringComparison.OrdinalIgnoreCase) ||
                 cmd.UserRole.Equals("EditorialBoard", StringComparison.OrdinalIgnoreCase) ||
                 cmd.UserRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = true;
        }

        if (!isAuthorized)
        {
            throw new UnauthorizedAccessException("You are not authorized to post comments on this task.");
        }

        var user = await _userRepo.GetByIdAsync(cmd.UserId, ct)
            ?? throw new KeyNotFoundException($"User {cmd.UserId} not found.");

        var userFullName = user.FullName ?? user.PenName ?? user.Username;

        var comment = TaskComment.Create(cmd.PageTaskId, cmd.UserId, userFullName, cmd.Content);

        await _commentRepo.AddAsync(comment, ct);
        await _commentRepo.SaveChangesAsync(ct);

        return new TaskCommentDto(
            comment.Id,
            comment.PageTaskId,
            comment.UserId,
            comment.UserFullName,
            comment.Content,
            comment.CreatedAt);
    }
}

public class AddCommentValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentValidator()
    {
        RuleFor(x => x.PageTaskId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(2000);
    }
}
