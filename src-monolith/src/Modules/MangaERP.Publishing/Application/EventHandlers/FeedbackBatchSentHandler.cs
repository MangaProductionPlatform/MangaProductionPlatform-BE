using MediatR;
using MangaERP.QA.Application.Commands;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Publishing.Domain.Entities;
using System.Threading;

namespace MangaERP.Publishing.Application.EventHandlers;

public class FeedbackBatchSentHandler : INotificationHandler<FeedbackBatchSentNotification>
{
    private readonly INotificationRepository _notificationRepo;

    public FeedbackBatchSentHandler(INotificationRepository notificationRepo)
    {
        _notificationRepo = notificationRepo;
    }

    public async System.Threading.Tasks.Task Handle(FeedbackBatchSentNotification notification, CancellationToken cancellationToken)
    {
        var dbNotification = new Notification
        {
            ReceiverId = notification.MangakaUserId,
            Title = "Chapter QA Feedback Received",
            Message = $"Chương truyện của bạn đã nhận được phản hồi sửa lỗi mới (Batch: {notification.BatchToken.ToString()[..8]}). Vui lòng sửa lại các lỗi đã ghim.",
            IsRead = false,
            NotifyType = "QA_Feedback",
            RelatedEntityId = notification.ChapterId,
            RelatedEntityType = "Chapter",
            CreatedAt = DateTime.UtcNow
        };

        await _notificationRepo.AddAsync(dbNotification, cancellationToken);
        await _notificationRepo.SaveChangesAsync(cancellationToken);
    }
}
