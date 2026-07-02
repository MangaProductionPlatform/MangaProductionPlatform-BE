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
            TargetUrl = $"/mangaka/series/{seriesId}"
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
            TargetUrl = $"/mangaka/submissions"
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

    public async System.Threading.Tasks.Task NotifyChapterPublishedAsync(
        Guid mangakaId, Guid chapterId, string chapterTitle,
        string publicationUrl, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            ReceiverId = mangakaId,
            Title = "Chương truyện của bạn đã được xuất bản!",
            Message = $"Chương \"{chapterTitle}\" đã chính thức được phát hành.",
            NotifyType = "ChapterPublished",
            RelatedEntityId = chapterId,
            RelatedEntityType = "Chapter",
            TargetUrl = publicationUrl
        };

        await _notificationRepo.AddAsync(notification, ct);
        await _notificationRepo.SaveChangesAsync(ct);

        await _hubContext.Clients.User(mangakaId.ToString())
            .SendAsync("ReceiveNotification", new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                notifyType = notification.NotifyType,
                chapterId,
                chapterTitle,
                targetUrl = notification.TargetUrl,
                createdAt = notification.CreatedAt
            }, ct);
    }

    // ── SUBMISSION WORKFLOW NOTIFICATIONS (Giai đoạn 1) ──────────────────────

    /// <summary>
    /// [Mốc 1] Broadcast tới toàn bộ EDITORIAL_BOARD khi Mangaka Submit / Re-Submit.
    /// Lưu in-app notification cho từng editor + push SignalR realtime.
    /// </summary>
    public async System.Threading.Tasks.Task NotifyNewSubmissionToEditorialBoardAsync(
        Guid submissionId, string submissionTitle, string authorName,
        CancellationToken ct = default)
    {
        var editorialBoardMembers = await _userRepo.GetByRoleAsync(UserRole.EditorialBoard, ct);
        var members = editorialBoardMembers.ToList();
        if (!members.Any()) return;

        var title   = "Bản thảo mới chờ duyệt";
        var message = $"Bản thảo mới: \"{submissionTitle}\" vừa được Tác giả {authorName} nộp lên hệ thống. " +
                      "Mời hội đồng vào đánh giá và bỏ phiếu!";

        foreach (var member in members)
        {
            var notification = new Notification
            {
                ReceiverId        = member.Id,
                Title             = title,
                Message           = message,
                NotifyType        = "NewSubmissionPendingReview",
                RelatedEntityId   = submissionId,
                RelatedEntityType = "Submission",
                TargetUrl         = "/app/board/submissions"
            };

            await _notificationRepo.AddAsync(notification, ct);

            await _hubContext.Clients.User(member.Id.ToString())
                .SendAsync("ReceiveNotification", new
                {
                    id          = notification.Id,
                    title       = notification.Title,
                    message     = notification.Message,
                    notifyType  = notification.NotifyType,
                    submissionId,
                    submissionTitle,
                    authorName,
                    targetUrl   = notification.TargetUrl,
                    createdAt   = notification.CreatedAt
                }, ct);
        }

        await _notificationRepo.SaveChangesAsync(ct);
    }

    /// <summary>
    /// [Mốc 2] Gửi In-app cho các EB members chưa vote khi có phiếu mới được cast (< 3 tổng).
    /// Danh sách remainingEditorIds được tính toán bởi CastVoteHandler — không lộ thông tin cho Mangaka.
    /// </summary>
    public async System.Threading.Tasks.Task NotifyVoteCastToRemainingEditorsAsync(
        Guid submissionId, string submissionTitle, string voterName,
        int currentVoteCount, int totalRequired,
        IEnumerable<Guid> remainingEditorIds, CancellationToken ct = default)
    {
        var remainingIds = remainingEditorIds.ToList();
        if (!remainingIds.Any()) return;

        var title   = "Phiếu bầu mới trên bản thảo";
        var message = $"Bản thảo \"{submissionTitle}\" đã nhận được phiếu bầu từ {voterName}. " +
                      $"Hệ thống đang chờ thêm các phiếu bầu còn lại (Hiện tại: {currentVoteCount}/{totalRequired}).";

        foreach (var editorId in remainingIds)
        {
            var notification = new Notification
            {
                ReceiverId        = editorId,
                Title             = title,
                Message           = message,
                NotifyType        = "SubmissionVoteCast",
                RelatedEntityId   = submissionId,
                RelatedEntityType = "Submission",
                TargetUrl         = "/app/board/submissions"
            };

            await _notificationRepo.AddAsync(notification, ct);

            await _hubContext.Clients.User(editorId.ToString())
                .SendAsync("ReceiveNotification", new
                {
                    id               = notification.Id,
                    title            = notification.Title,
                    message          = notification.Message,
                    notifyType       = notification.NotifyType,
                    submissionId,
                    submissionTitle,
                    voterName,
                    currentVoteCount,
                    totalRequired,
                    targetUrl        = notification.TargetUrl,
                    createdAt        = notification.CreatedAt
                }, ct);
        }

        await _notificationRepo.SaveChangesAsync(ct);
    }

    /// <summary>
    /// [Mốc 4] Push khẩn cấp tới toàn bộ EDITOR_IN_CHIEF khi xảy ra tranh chấp 1-1-1.
    /// KHÔNG gửi cho Mangaka — trên UI tác giả vẫn hiển thị "Đang chờ duyệt".
    /// </summary>
    public async System.Threading.Tasks.Task NotifyConflictEscalatedToEicAsync(
        Guid submissionId, string submissionTitle, string authorName,
        CancellationToken ct = default)
    {
        var eicMembers = await _userRepo.GetByRoleAsync(UserRole.EditorInChief, ct);
        var members = eicMembers.ToList();
        if (!members.Any()) return;

        var title   = "⚠️ CẢNH BÁO TRANH CHẤP — Cần phân xử";
        var message = $"CẢNH BÁO TRANH CHẤP: Bản thảo \"{submissionTitle}\" của Tác giả {authorName} " +
                      "bất phân thắng bại (1-1-1) sau khi hội đồng bỏ phiếu. " +
                      "Mời Tổng biên tập vào phân xử!";

        foreach (var eic in members)
        {
            var notification = new Notification
            {
                ReceiverId        = eic.Id,
                Title             = title,
                Message           = message,
                NotifyType        = "SubmissionConflictEscalated",
                RelatedEntityId   = submissionId,
                RelatedEntityType = "Submission",
                TargetUrl         = $"/app/board/submissions?filter=conflict&id={submissionId}"
            };

            await _notificationRepo.AddAsync(notification, ct);

            await _hubContext.Clients.User(eic.Id.ToString())
                .SendAsync("ReceiveNotification", new
                {
                    id          = notification.Id,
                    title       = notification.Title,
                    message     = notification.Message,
                    notifyType  = notification.NotifyType,
                    submissionId,
                    submissionTitle,
                    authorName,
                    urgent      = true,
                    targetUrl   = notification.TargetUrl,
                    createdAt   = notification.CreatedAt
                }, ct);
        }

        await _notificationRepo.SaveChangesAsync(ct);
    }

    /// <summary>
    /// [Mốc 3/5] Thông báo cho Tantou Editor được gán phụ trách tác phẩm mới sau khi Approve.
    /// Gửi sau khi load-balancing chọn xong TE và MangaSeries đã được tạo.
    /// </summary>
    public async System.Threading.Tasks.Task NotifyTantouEditorAssignedAsync(
        Guid tantouEditorId, Guid submissionId, string seriesTitle,
        string authorName, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            ReceiverId        = tantouEditorId,
            Title             = "Bạn được chỉ định phụ trách tác phẩm mới",
            Message           = $"Bạn được chỉ định phụ trách tác phẩm mới \"{seriesTitle}\" " +
                                $"của Tác giả {authorName}. Vui lòng liên hệ để bắt đầu sản xuất.",
            NotifyType        = "TantouEditorAssigned",
            RelatedEntityId   = submissionId,
            RelatedEntityType = "Submission",
            TargetUrl         = "/app/editor/dashboard"
        };

        await _notificationRepo.AddAsync(notification, ct);
        await _notificationRepo.SaveChangesAsync(ct);

        await _hubContext.Clients.User(tantouEditorId.ToString())
            .SendAsync("ReceiveNotification", new
            {
                id          = notification.Id,
                title       = notification.Title,
                message     = notification.Message,
                notifyType  = notification.NotifyType,
                submissionId,
                seriesTitle,
                authorName,
                targetUrl   = notification.TargetUrl,
                createdAt   = notification.CreatedAt
            }, ct);
    }

    public async System.Threading.Tasks.Task NotifySegmentationTaskAssignedAsync(
        Guid assistantId, Guid segmentationTaskId, string taskType,
        CancellationToken ct = default)
    {
        var notification = new Notification
        {
            ReceiverId        = assistantId,
            Title             = "Bạn được giao một nhiệm vụ phân vùng mới",
            Message           = $"Bạn vừa được giao một Segmentation Task loại '{taskType}'. Hãy kiểm tra workspace để bắt đầu.",
            NotifyType        = "SegmentationTaskAssigned",
            RelatedEntityId   = segmentationTaskId,
            RelatedEntityType = "SegmentationTask",
            TargetUrl         = "/app/assistant/tasks"
        };

        await _notificationRepo.AddAsync(notification, ct);
        await _notificationRepo.SaveChangesAsync(ct);

        await _hubContext.Clients.User(assistantId.ToString())
            .SendAsync("ReceiveNotification", new
            {
                id                  = notification.Id,
                title               = notification.Title,
                message             = notification.Message,
                notifyType          = notification.NotifyType,
                segmentationTaskId,
                taskType,
                targetUrl           = notification.TargetUrl,
                createdAt           = notification.CreatedAt
            }, ct);
    }
}
