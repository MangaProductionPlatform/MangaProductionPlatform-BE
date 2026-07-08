using MangaERP.Shared.Application.Contracts.Events;
using MangaERP.Shared.Application.Ports;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MangaERP.Task.Application.EventHandlers;

/// <summary>
/// Lắng nghe sự kiện <see cref="SegmentationTaskAssignedEvent"/> do module Segmentation phát ra.
/// Khi một Segmentation Task mới được tạo và giao cho Assistant, handler này sẽ:
///   1. Lưu bản ghi Notification vào DB.
///   2. Push thông báo real-time qua SignalR Hub (/hubs/notifications) tới Assistant được giao việc.
/// Việc phân tách sang module Task/Notification đảm bảo module Segmentation hoàn toàn cô lập
/// — nó chỉ phát sự kiện, không phụ thuộc vào bất kỳ module nào khác.
/// </summary>
public class SegmentationTaskAssignedHandler : INotificationHandler<SegmentationTaskAssignedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<SegmentationTaskAssignedHandler> _logger;

    public SegmentationTaskAssignedHandler(
        INotificationService notificationService,
        ILogger<SegmentationTaskAssignedHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task Handle(
        SegmentationTaskAssignedEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.NotifySegmentationTaskAssignedAsync(
                assistantId:        notification.AssignedToUserId,
                segmentationTaskId: notification.TaskId,
                taskType:           notification.TaskType,
                ct:                 cancellationToken);

            _logger.LogInformation(
                "[SegmentationEvent] Sent notification to assistant {AssistantId} for SegmentationTask {TaskId} (type: {TaskType}).",
                notification.AssignedToUserId,
                notification.TaskId,
                notification.TaskType);
        }
        catch (Exception ex)
        {
            // Không ném lỗi ra ngoài để tránh làm rollback transaction của Segmentation module.
            // Lỗi thông báo không được phép chặn luồng tạo task chính.
            _logger.LogError(ex,
                "[SegmentationEvent] Failed to send notification to assistant {AssistantId} for SegmentationTask {TaskId}.",
                notification.AssignedToUserId,
                notification.TaskId);
        }
    }
}
