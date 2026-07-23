using MediatR;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;

namespace MangaERP.Studio.Application.Commands.ManageCollaboration;

public record SuspendCollaborationCommand(Guid CollaborationId, Guid ActorUserId, bool IsAdmin,
    CollaborationSuspensionMode Mode, string Reason, Guid ExpectedConcurrencyToken) : IRequest<Unit>;
public record ChangeSuspensionModeCommand(Guid CollaborationId, Guid ActorUserId, bool IsAdmin,
    CollaborationSuspensionMode Mode, string Reason, Guid ExpectedConcurrencyToken) : IRequest<Unit>;
public record ReactivateCollaborationCommand(Guid CollaborationId, Guid ActorUserId, bool IsAdmin,
    Guid ExpectedConcurrencyToken) : IRequest<Unit>;
public record RequestEndingCollaborationCommand(Guid CollaborationId, Guid ActorUserId, bool IsAdmin,
    Guid ExpectedConcurrencyToken) : IRequest<Unit>;
public record EndCollaborationCommand(Guid CollaborationId, Guid ActorUserId, bool IsAdmin,
    string Reason, Guid ExpectedConcurrencyToken) : IRequest<Unit>;

internal static class CollaborationCommandSupport
{
    public static MangakaAssistantCollaboration Authorize(MangakaAssistantCollaboration? collaboration, Guid actor, bool admin)
    {
        if (collaboration is null) throw new EntityNotFoundException("Collaboration", Guid.Empty);
        if (!admin && collaboration.MangakaId != actor) throw new UnauthorizedAccessException("You cannot manage this collaboration.");
        return collaboration;
    }

    public static void CheckVersion(MangakaAssistantCollaboration collaboration, Guid expected)
    {
        if (expected == Guid.Empty || collaboration.ConcurrencyToken != expected)
            throw new ConflictException("The collaboration changed concurrently. Refresh and retry.");
    }
}

public sealed class SuspendCollaborationHandler : IRequestHandler<SuspendCollaborationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly IStudioTaskRevocationService _taskRevocationService;
    private readonly INotificationService _notifications;

    public SuspendCollaborationHandler(
        IStudioInvitationRepository repo,
        IStudioTaskRevocationService taskRevocationService,
        INotificationService notifications)
    {
        _repo = repo;
        _taskRevocationService = taskRevocationService;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(SuspendCollaborationCommand request, CancellationToken ct)
    {
        var collaboration = CollaborationCommandSupport.Authorize(await _repo.GetCollaborationAsync(request.CollaborationId, ct), request.ActorUserId, request.IsAdmin);
        CollaborationCommandSupport.CheckVersion(collaboration, request.ExpectedConcurrencyToken);

        collaboration.Suspend(request.Mode, request.Reason, DateTime.UtcNow);

        await _taskRevocationService.HandleCollaborationStateChangeAsync(
            collaboration.Id, CollaborationStatus.Suspended, request.Mode, request.ActorUserId, ct);

        await _repo.AddCollaborationEventAsync(new CollaborationEvent(collaboration.Id, CollaborationEventType.CollaborationSuspended,
            request.ActorUserId, DateTime.UtcNow, request.Reason, $"{{\"mode\":\"{request.Mode}\"}}"), ct);

        await _repo.SaveChangesAsync(ct);
        await NotifySafe(_notifications, collaboration, "CollaborationSuspended", "Collaboration suspended", request.Reason, ct);
        return Unit.Value;
    }

    internal static async Task NotifySafe(INotificationService notifications, MangakaAssistantCollaboration c, string type, string title, string message, CancellationToken ct)
    {
        await notifications.NotifyCollaborationEventAsync(c.AssistantId, type, title, message, c.Id, ct);
        await notifications.NotifyCollaborationEventAsync(c.MangakaId, type, title, message, c.Id, ct);
    }
}

public sealed class ChangeSuspensionModeHandler : IRequestHandler<ChangeSuspensionModeCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly IStudioTaskRevocationService _taskRevocationService;
    private readonly INotificationService _notifications;

    public ChangeSuspensionModeHandler(
        IStudioInvitationRepository repo,
        IStudioTaskRevocationService taskRevocationService,
        INotificationService notifications)
    {
        _repo = repo;
        _taskRevocationService = taskRevocationService;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(ChangeSuspensionModeCommand request, CancellationToken ct)
    {
        var c = CollaborationCommandSupport.Authorize(await _repo.GetCollaborationAsync(request.CollaborationId, ct), request.ActorUserId, request.IsAdmin);
        CollaborationCommandSupport.CheckVersion(c, request.ExpectedConcurrencyToken);

        c.ChangeSuspensionMode(request.Mode, request.Reason, DateTime.UtcNow);

        await _taskRevocationService.HandleCollaborationStateChangeAsync(
            c.Id, CollaborationStatus.Suspended, request.Mode, request.ActorUserId, ct);

        await _repo.AddCollaborationEventAsync(new CollaborationEvent(c.Id, CollaborationEventType.SuspensionModeChanged,
            request.ActorUserId, DateTime.UtcNow, request.Reason, $"{{\"mode\":\"{request.Mode}\"}}"), ct);

        await _repo.SaveChangesAsync(ct);
        await SuspendCollaborationHandler.NotifySafe(_notifications, c, "SuspensionModeChanged", "Suspension mode changed", request.Reason, ct);
        return Unit.Value;
    }
}

public sealed class ReactivateCollaborationHandler : IRequestHandler<ReactivateCollaborationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly INotificationService _notifications;

    public ReactivateCollaborationHandler(IStudioInvitationRepository repo, INotificationService notifications)
    {
        _repo = repo;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(ReactivateCollaborationCommand request, CancellationToken ct)
    {
        var c = CollaborationCommandSupport.Authorize(await _repo.GetCollaborationAsync(request.CollaborationId, ct), request.ActorUserId, request.IsAdmin);
        CollaborationCommandSupport.CheckVersion(c, request.ExpectedConcurrencyToken);
        c.Reactivate(DateTime.UtcNow);

        await _repo.AddCollaborationEventAsync(new CollaborationEvent(c.Id, CollaborationEventType.CollaborationReactivated, request.ActorUserId, DateTime.UtcNow), ct);
        await _repo.SaveChangesAsync(ct);
        await SuspendCollaborationHandler.NotifySafe(_notifications, c, "CollaborationReactivated", "Collaboration reactivated", "The collaboration is active again.", ct);
        return Unit.Value;
    }
}

public sealed class RequestEndingCollaborationHandler : IRequestHandler<RequestEndingCollaborationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly IStudioTaskRevocationService _taskRevocationService;
    private readonly INotificationService _notifications;

    public RequestEndingCollaborationHandler(
        IStudioInvitationRepository repo,
        IStudioTaskRevocationService taskRevocationService,
        INotificationService notifications)
    {
        _repo = repo;
        _taskRevocationService = taskRevocationService;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(RequestEndingCollaborationCommand request, CancellationToken ct)
    {
        var c = CollaborationCommandSupport.Authorize(await _repo.GetCollaborationAsync(request.CollaborationId, ct), request.ActorUserId, request.IsAdmin);
        CollaborationCommandSupport.CheckVersion(c, request.ExpectedConcurrencyToken);

        c.RequestEnding(DateTime.UtcNow);

        await _taskRevocationService.HandleCollaborationStateChangeAsync(
            c.Id, CollaborationStatus.EndingRequested, null, request.ActorUserId, ct);

        await _repo.AddCollaborationEventAsync(new CollaborationEvent(c.Id, CollaborationEventType.EndingRequested, request.ActorUserId, DateTime.UtcNow), ct);
        await _repo.SaveChangesAsync(ct);
        await SuspendCollaborationHandler.NotifySafe(_notifications, c, "CollaborationEndingRequested", "Ending Requested", "Ending has been requested for this collaboration.", ct);
        return Unit.Value;
    }
}

public sealed class EndCollaborationHandler : IRequestHandler<EndCollaborationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly IStudioTaskRevocationService _taskRevocationService;
    private readonly INotificationService _notifications;

    public EndCollaborationHandler(
        IStudioInvitationRepository repo,
        IStudioTaskRevocationService taskRevocationService,
        INotificationService notifications)
    {
        _repo = repo;
        _taskRevocationService = taskRevocationService;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(EndCollaborationCommand request, CancellationToken ct)
    {
        var c = CollaborationCommandSupport.Authorize(await _repo.GetCollaborationAsync(request.CollaborationId, ct), request.ActorUserId, request.IsAdmin);
        CollaborationCommandSupport.CheckVersion(c, request.ExpectedConcurrencyToken);

        c.End(request.Reason, request.ActorUserId, DateTime.UtcNow);

        await _taskRevocationService.HandleCollaborationStateChangeAsync(
            c.Id, CollaborationStatus.Ended, null, request.ActorUserId, ct);

        await _repo.AddCollaborationEventAsync(new CollaborationEvent(c.Id, CollaborationEventType.CollaborationEnded, request.ActorUserId, DateTime.UtcNow, request.Reason), ct);
        await _repo.SaveChangesAsync(ct);
        await SuspendCollaborationHandler.NotifySafe(_notifications, c, "CollaborationEnded", "Collaboration Ended", request.Reason, ct);
        return Unit.Value;
    }
}
