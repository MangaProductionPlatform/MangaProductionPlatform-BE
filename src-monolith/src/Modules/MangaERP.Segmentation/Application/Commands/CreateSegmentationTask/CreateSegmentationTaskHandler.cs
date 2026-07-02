using FluentValidation;
using MangaERP.Shared.Application.Contracts.Events;
using MangaERP.Segmentation.Application.Ports;
using MangaERP.Segmentation.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MangaERP.Segmentation.Application.Commands.CreateSegmentationTask;

public record CreateSegmentationTaskCommand(
    Guid PageId,
    string MaskRle,
    int[] Bbox,
    SegmentationTaskType TaskType,
    string? Note,
    Guid AssignedToUserId,
    string? AssignedToUserRole,
    Guid CreatedByUserId
) : IRequest<CreateSegmentationTaskResult>;

public record CreateSegmentationTaskResult(Guid TaskId, string Status);

public class CreateSegmentationTaskHandler
    : IRequestHandler<CreateSegmentationTaskCommand, CreateSegmentationTaskResult>
{
    private readonly ISegmentationTaskRepository _repo;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateSegmentationTaskHandler> _logger;

    public CreateSegmentationTaskHandler(
        ISegmentationTaskRepository repo,
        IMediator mediator,
        ILogger<CreateSegmentationTaskHandler> logger)
    {
        _repo = repo;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<CreateSegmentationTaskResult> Handle(
        CreateSegmentationTaskCommand cmd,
        CancellationToken ct)
    {
        var task = new SegmentationTask
        {
            PageId             = cmd.PageId,
            MaskRle            = cmd.MaskRle,
            Bbox               = cmd.Bbox,
            TaskType           = cmd.TaskType,
            Note               = cmd.Note,
            AssignedToUserId   = cmd.AssignedToUserId,
            AssignedToUserRole = cmd.AssignedToUserRole,
            CreatedByUserId    = cmd.CreatedByUserId,
            Status             = SegmentationTaskStatus.Pending,
            CreatedAt          = DateTime.UtcNow
        };

        await _repo.AddAsync(task, ct);
        await _repo.SaveChangesAsync(ct);

        try
        {
            await _mediator.Publish(new SegmentationTaskAssignedEvent(
                task.Id,
                task.AssignedToUserId,
                task.CreatedByUserId,
                task.PageId,
                task.TaskType.ToString()), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Segmentation] Failed to publish SegmentationTaskAssignedEvent for task {TaskId}. Task was saved successfully.",
                task.Id);
        }

        return new CreateSegmentationTaskResult(task.Id, task.Status.ToString());
    }
}

public class CreateSegmentationTaskValidator : AbstractValidator<CreateSegmentationTaskCommand>
{
    public CreateSegmentationTaskValidator()
    {
        RuleFor(x => x.PageId).NotEmpty()
            .WithMessage("PageId is required.");

        RuleFor(x => x.MaskRle).NotEmpty()
            .WithMessage("MaskRle is required and must not be empty.");

        RuleFor(x => x.AssignedToUserId).NotEmpty()
            .WithMessage("AssignedToUserId is required.");

        RuleFor(x => x.CreatedByUserId).NotEmpty()
            .WithMessage("CreatedByUserId is required.");

        RuleFor(x => x.TaskType).IsInEnum()
            .WithMessage("TaskType must be a valid SegmentationTaskType value.");

        RuleFor(x => x.Bbox)
            .Must(b => b is { Length: 4 })
            .WithMessage("Bbox must have exactly 4 elements [x1, y1, x2, y2].")
            .When(x => x.Bbox is { Length: > 0 });
    }
}
