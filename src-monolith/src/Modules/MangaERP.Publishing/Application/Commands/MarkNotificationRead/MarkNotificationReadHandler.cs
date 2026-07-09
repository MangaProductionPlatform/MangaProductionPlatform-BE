using MediatR;
using MangaERP.Publishing.Application.Ports;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Publishing.Application.Commands.MarkNotificationRead;

public record MarkNotificationReadCommand(
    Guid NotificationId,
    Guid RequesterId
) : IRequest<bool>;

public class MarkNotificationReadHandler
    : IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationRepository _repo;

    public MarkNotificationReadHandler(INotificationRepository repo)
    {
        _repo = repo;
    }

    public async Task<bool> Handle(MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        var notification = await _repo.GetByIdAsync(cmd.NotificationId, ct)
            ?? throw new KeyNotFoundException($"Notification {cmd.NotificationId} not found.");

        if (notification.ReceiverId != cmd.RequesterId)
            throw new UnauthorizedAccessException("Bạn không có quyền đánh dấu thông báo này.");

        notification.IsRead = true;
        await _repo.UpdateAsync(notification, ct);
        // NOTE: UpdateAsync already calls SaveChangesAsync internally — no need to call again.
        return true;
    }
}
