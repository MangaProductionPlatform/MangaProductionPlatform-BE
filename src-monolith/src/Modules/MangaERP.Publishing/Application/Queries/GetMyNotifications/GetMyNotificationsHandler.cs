using MediatR;
using MangaERP.Publishing.Application.Ports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Publishing.Application.Queries.GetMyNotifications;

public record GetMyNotificationsQuery(
    Guid ReceiverId,
    bool UnreadOnly = false
) : IRequest<IEnumerable<NotificationDto>>;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    bool IsRead,
    string NotifyType,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    string? TargetUrl,
    DateTime CreatedAt);

public class GetMyNotificationsHandler
    : IRequestHandler<GetMyNotificationsQuery, IEnumerable<NotificationDto>>
{
    private readonly INotificationRepository _repo;

    public GetMyNotificationsHandler(INotificationRepository repo)
    {
        _repo = repo;
    }

    public async Task<IEnumerable<NotificationDto>> Handle(
        GetMyNotificationsQuery query, CancellationToken ct)
    {
        var notifications = query.UnreadOnly
            ? await _repo.GetUnreadByReceiverAsync(query.ReceiverId, ct)
            : await _repo.GetAllByReceiverAsync(query.ReceiverId, ct);

        return notifications.Select(n => new NotificationDto(
            n.Id,
            n.Title,
            n.Message,
            n.IsRead,
            n.NotifyType,
            n.RelatedEntityId,
            n.RelatedEntityType,
            n.TargetUrl,
            n.CreatedAt));
    }
}
