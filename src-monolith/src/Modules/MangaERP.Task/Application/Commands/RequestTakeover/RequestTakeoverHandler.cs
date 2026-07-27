using System;
using System.Threading;
using System.Threading.Tasks;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Task.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Commands.RequestTakeover;

public record RequestTakeoverCommand(
    Guid PageTaskId,
    Guid ActorUserId,
    string Reason,
    TimeSpan? WorkDuration = null
) : IRequest<RequestTakeoverResult>;

public record RequestTakeoverResult(
    Guid PageTaskId,
    Guid BackupAssistantId,
    Guid AttemptId,
    string TakeoverStatus,
    DateTime ResponseDeadline
);

public class RequestTakeoverHandler : IRequestHandler<RequestTakeoverCommand, RequestTakeoverResult>
{
    public RequestTakeoverHandler() { }

    public RequestTakeoverHandler(
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        INotificationService notificationService) { }

    public Task<RequestTakeoverResult> Handle(RequestTakeoverCommand request, CancellationToken ct)
    {
        throw new NotSupportedException("Takeover workflow has been retired. Please use the Reassign API (POST /api/v1/tasks/{taskId}/reassign) instead.");
    }
}
