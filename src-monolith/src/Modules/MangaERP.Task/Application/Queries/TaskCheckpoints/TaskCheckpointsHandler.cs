using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using System.Threading.Tasks;

namespace MangaERP.Task.Application.Queries.TaskCheckpoints;

public record GetTaskCheckpointsQuery(Guid TaskId, Guid ActorUserId) : IRequest<IEnumerable<TaskCheckpointDto>>;

public record TaskCheckpointDto(
    Guid Id,
    Guid TaskId,
    string Name,
    int TargetPercent,
    int OffsetMinutesFromAcceptance,
    DateTime? DueAt,
    int LatestProgressPercent,
    string Status);

public sealed class GetTaskCheckpointsHandler : IRequestHandler<GetTaskCheckpointsQuery, IEnumerable<TaskCheckpointDto>>
{
    private readonly IPageTaskRepository _taskRepo;
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly ITaskCheckpointRepository _checkpointRepo;
    private readonly ICollaborationAuthorizationService _authService;

    public GetTaskCheckpointsHandler(
        IPageTaskRepository taskRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        ITaskCheckpointRepository checkpointRepo,
        ICollaborationAuthorizationService authService)
    {
        _taskRepo = taskRepo;
        _attemptRepo = attemptRepo;
        _checkpointRepo = checkpointRepo;
        _authService = authService;
    }

    public async Task<IEnumerable<TaskCheckpointDto>> Handle(GetTaskCheckpointsQuery request, CancellationToken ct)
    {
        bool canAccess = await _authService.CanAccessTaskAsync(request.ActorUserId, request.TaskId, ct);
        if (!canAccess)
            throw new UnauthorizedAccessException("You are not authorized to view checkpoints for this task.");

        var task = await _taskRepo.GetByIdAsync(request.TaskId, ct);
        if (task == null) return Enumerable.Empty<TaskCheckpointDto>();

        var acceptedAttempt = await _attemptRepo.GetAcceptedByTaskIdAsync(request.TaskId, ct);
        DateTime? acceptedAt = acceptedAttempt?.AcceptedAt;
        DateTime now = DateTime.UtcNow;

        var checkpoints = await _checkpointRepo.GetByTaskIdAsync(request.TaskId, ct);

        return checkpoints.Select(c =>
        {
            DateTime? dueAt = acceptedAt.HasValue ? acceptedAt.Value.AddMinutes(c.OffsetMinutesFromAcceptance) : null;
            var status = c.ComputeStatus(acceptedAt, task.ProgressPercent, now).ToString();

            return new TaskCheckpointDto(
                c.Id,
                c.TaskId,
                c.Name,
                c.TargetPercent,
                c.OffsetMinutesFromAcceptance,
                dueAt,
                task.ProgressPercent,
                status);
        });
    }
}
