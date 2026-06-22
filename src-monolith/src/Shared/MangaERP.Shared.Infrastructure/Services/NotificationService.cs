using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MangaERP.Shared.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly IUserRepository _userRepo;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(
        INotificationRepository notificationRepo,
        IUserRepository userRepo,
        IHubContext<NotificationHub> hubContext)
    {
        _notificationRepo = notificationRepo;
        _userRepo = userRepo;
        _hubContext = hubContext;
    }

    public async System.Threading.Tasks.Task NotifyTaskAssignedAsync(
        Guid assistantId, Guid pageTaskId, int pageNumber, CancellationToken ct = default)
    {
        await _notificationRepo.AddAsync(new Notification
        {
            ReceiverId = assistantId,
            Title = "New page task assigned",
            Message = $"You have been assigned page {pageNumber}.",
            NotifyType = "TaskAssigned",
            RelatedEntityId = pageTaskId,
            RelatedEntityType = "PageTask"
        }, ct);
        await _notificationRepo.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task NotifyRevisionRequiredAsync(
        Guid assistantId, Guid pageTaskId, string rejectionNote, CancellationToken ct = default)
    {
        await _notificationRepo.AddAsync(new Notification
        {
            ReceiverId = assistantId,
            Title = "Revision required",
            Message = rejectionNote,
            NotifyType = "RevisionRequired",
            RelatedEntityId = pageTaskId,
            RelatedEntityType = "PageTask"
        }, ct);
        await _notificationRepo.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task NotifyTaskApprovedAsync(
        Guid assistantId, Guid pageTaskId, CancellationToken ct = default)
    {
        await _notificationRepo.AddAsync(new Notification
        {
            ReceiverId = assistantId,
            Title = "Layer approved",
            Message = "Your artwork layer has been approved.",
            NotifyType = "TaskApproved",
            RelatedEntityId = pageTaskId,
            RelatedEntityType = "PageTask"
        }, ct);
        await _notificationRepo.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task NotifyChapterReadyForQAAsync(
        Guid chapterId, string chapterTitle, CancellationToken ct = default)
    {
        var editors = await _userRepo.GetByRoleAsync(UserRole.TantouEditor, ct);
        foreach (var editor in editors)
        {
            await _notificationRepo.AddAsync(new Notification
            {
                ReceiverId = editor.Id,
                Title = "Chapter ready for QA",
                Message = $"Chapter \"{chapterTitle}\" is ready for editorial review.",
                NotifyType = "ChapterReadyForQA",
                RelatedEntityId = chapterId,
                RelatedEntityType = "Chapter"
            }, ct);
        }

        if (editors.Any())
            await _notificationRepo.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task NotifySubmissionRevisionAsync(
        Guid receiverId, Guid submissionId, string message,
        int pinCount, string? targetUrl, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            ReceiverId = receiverId,
            Title = $"Revision Required: {pinCount} feedback pin(s) on your manuscript",
            Message = message,
            NotifyType = "SubmissionRevisionRequired",
            RelatedEntityId = submissionId,
            RelatedEntityType = "Submission",
            TargetUrl = targetUrl
        };

        await _notificationRepo.AddAsync(notification, ct);
        await _notificationRepo.SaveChangesAsync(ct);

        await _hubContext.Clients.User(receiverId.ToString())
            .SendAsync("ReceiveNotification", new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                notifyType = notification.NotifyType,
                submissionId,
                pinCount,
                targetUrl,
                createdAt = notification.CreatedAt
            }, ct);
    }

    public async System.Threading.Tasks.Task NotifySubmissionApprovedAsync(
        Guid receiverId, Guid submissionId, Guid seriesId,
        string seriesTitle, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            ReceiverId = receiverId,
            Title = "Chúc mừng! Bản thảo của bạn đã được phê duyệt",
            Message = $"Series \"{seriesTitle}\" đã được kích hoạt. Bạn có thể bắt đầu tạo Chapter ngay bây giờ.",
            NotifyType = "SubmissionApproved",
            RelatedEntityId = submissionId,
            RelatedEntityType = "Submission",
            TargetUrl = $"/workspace/series/{seriesId}"
        };

        await _notificationRepo.AddAsync(notification, ct);
        await _notificationRepo.SaveChangesAsync(ct);

        await _hubContext.Clients.User(receiverId.ToString())
            .SendAsync("ReceiveNotification", new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                notifyType = notification.NotifyType,
                submissionId,
                seriesId,
                seriesTitle,
                targetUrl = notification.TargetUrl,
                createdAt = notification.CreatedAt
            }, ct);
    }

    public async System.Threading.Tasks.Task NotifySubmissionRejectedAsync(
        Guid receiverId, Guid submissionId, string feedbackMessage,
        CancellationToken ct = default)
    {
        var notification = new Notification
        {
            ReceiverId = receiverId,
            Title = "Bản thảo của bạn đã bị từ chối",
            Message = feedbackMessage,
            NotifyType = "SubmissionRejected",
            RelatedEntityId = submissionId,
            RelatedEntityType = "Submission",
            TargetUrl = $"/workspace/submissions/{submissionId}"
        };

        await _notificationRepo.AddAsync(notification, ct);
        await _notificationRepo.SaveChangesAsync(ct);

        await _hubContext.Clients.User(receiverId.ToString())
            .SendAsync("ReceiveNotification", new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                notifyType = notification.NotifyType,
                submissionId,
                feedbackMessage,
                targetUrl = notification.TargetUrl,
                createdAt = notification.CreatedAt
            }, ct);
    }
}
