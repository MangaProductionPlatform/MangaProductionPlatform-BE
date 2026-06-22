namespace MangaERP.Shared.Application.Ports;

public interface INotificationService
{
    Task NotifyTaskAssignedAsync(Guid assistantId, Guid pageTaskId, int pageNumber, CancellationToken ct = default);
    Task NotifyRevisionRequiredAsync(Guid assistantId, Guid pageTaskId, string rejectionNote, CancellationToken ct = default);
    Task NotifyTaskApprovedAsync(Guid assistantId, Guid pageTaskId, CancellationToken ct = default);
    Task NotifyChapterReadyForQAAsync(Guid chapterId, string chapterTitle, CancellationToken ct = default);

    /// <summary>
    /// Thông báo Mangaka về yêu cầu chỉnh sửa bản thảo kèm Visual Feedback Pins.
    /// Bao gồm deep-link URL trỏ tới canvas workspace.
    /// </summary>
    Task NotifySubmissionRevisionAsync(
        Guid receiverId, Guid submissionId, string message,
        int pinCount, string? targetUrl, CancellationToken ct = default);

    /// <summary>
    /// Thông báo Mangaka rằng bản thảo đã được Editorial Board phê duyệt.
    /// Bao gồm thông tin Series vừa được tạo.
    /// </summary>
    Task NotifySubmissionApprovedAsync(
        Guid receiverId, Guid submissionId, Guid seriesId,
        string seriesTitle, CancellationToken ct = default);

    /// <summary>
    /// Thông báo Mangaka rằng bản thảo đã bị Editorial Board từ chối chính thức.
    /// Bao gồm lý do từ chối.
    /// </summary>
    Task NotifySubmissionRejectedAsync(
        Guid receiverId, Guid submissionId, string feedbackMessage,
        CancellationToken ct = default);
}
