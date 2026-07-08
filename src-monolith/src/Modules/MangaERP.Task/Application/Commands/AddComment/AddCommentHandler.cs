using FluentValidation;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Task.Application.Queries.GetTaskComments;
using MediatR;

namespace MangaERP.Task.Application.Commands.AddComment;

public record AddCommentCommand(
    Guid PageTaskId,
    Guid UserId,
    string Content
) : IRequest<TaskCommentDto>;

public class AddCommentHandler : IRequestHandler<AddCommentCommand, TaskCommentDto>
{
    private readonly ITaskCommentRepository _commentRepo;
    private readonly IUserRepository _userRepo;

    public AddCommentHandler(ITaskCommentRepository commentRepo, IUserRepository userRepo)
    {
        _commentRepo = commentRepo;
        _userRepo = userRepo;
    }

    public async Task<TaskCommentDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
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
