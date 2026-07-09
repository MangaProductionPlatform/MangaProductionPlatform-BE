using FluentValidation;
using MangaERP.Segmentation.Application.Ports;
using MangaERP.Segmentation.Domain.Entities;
using MediatR;

namespace MangaERP.Segmentation.Application.Commands.UpdateSegmentationTaskStatus;

public record UpdateSegmentationTaskStatusCommand(
    Guid TaskId,
    Guid CallerUserId,
    SegmentationTaskStatus NewStatus
) : IRequest<UpdateSegmentationTaskStatusResult>;

public record UpdateSegmentationTaskStatusResult(Guid TaskId, string OldStatus, string NewStatus);

public class UpdateSegmentationTaskStatusHandler
    : IRequestHandler<UpdateSegmentationTaskStatusCommand, UpdateSegmentationTaskStatusResult>
{
    private readonly ISegmentationTaskRepository _repo;

    public UpdateSegmentationTaskStatusHandler(ISegmentationTaskRepository repo)
        => _repo = repo;

    public async Task<UpdateSegmentationTaskStatusResult> Handle(
        UpdateSegmentationTaskStatusCommand cmd,
        CancellationToken ct)
    {
        var task = await _repo.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new KeyNotFoundException($"SegmentationTask {cmd.TaskId} not found.");

        // Check ownership: caller must be the assigned user
        if (task.AssignedToUserId != cmd.CallerUserId)
            throw new UnauthorizedAccessException(
                $"User {cmd.CallerUserId} is not the assigned user for task {cmd.TaskId}.");

        var oldStatus = task.Status.ToString();

        // Enforce state machine transition
        task.TransitionTo(cmd.NewStatus);

        await _repo.UpdateAsync(task, ct);
        await _repo.SaveChangesAsync(ct);

        return new UpdateSegmentationTaskStatusResult(task.Id, oldStatus, task.Status.ToString());
    }
}

public class UpdateSegmentationTaskStatusValidator
    : AbstractValidator<UpdateSegmentationTaskStatusCommand>
{
    public UpdateSegmentationTaskStatusValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}
