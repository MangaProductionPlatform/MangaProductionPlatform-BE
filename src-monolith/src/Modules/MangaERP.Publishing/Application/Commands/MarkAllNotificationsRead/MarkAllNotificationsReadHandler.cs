using MediatR;
using MangaERP.Publishing.Application.Ports;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MangaERP.Publishing.Application.Commands.MarkAllNotificationsRead;

/// <summary>
/// Đánh dấu tất cả thông báo chưa đọc của người dùng hiện tại là đã đọc.
/// Dùng cho nút "Đọc tất cả" trên UI notification panel.
/// Trả về số thông báo được cập nhật.
/// </summary>
public record MarkAllNotificationsReadCommand(
    Guid RequesterId
) : IRequest<int>;

public class MarkAllNotificationsReadHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly INotificationRepository _repo;

    public MarkAllNotificationsReadHandler(INotificationRepository repo)
    {
        _repo = repo;
    }

    public async Task<int> Handle(MarkAllNotificationsReadCommand cmd, CancellationToken ct)
    {
        // Bulk UPDATE — 1 SQL statement, không loop từng entity
        var updatedCount = await _repo.MarkAllAsReadAsync(cmd.RequesterId, ct);
        return updatedCount;
    }
}
