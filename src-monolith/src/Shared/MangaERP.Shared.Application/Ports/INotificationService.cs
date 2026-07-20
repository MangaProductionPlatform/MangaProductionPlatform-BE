namespace MangaERP.Shared.Application.Ports;

public interface INotificationService
{
    Task NotifyTaskAssignedAsync(Guid assistantId, Guid pageTaskId, int pageNumber, CancellationToken ct = default);
    Task NotifyRevisionRequiredAsync(Guid assistantId, Guid pageTaskId, string rejectionNote, CancellationToken ct = default);
    Task NotifyTaskApprovedAsync(Guid assistantId, Guid pageTaskId, CancellationToken ct = default);
    Task NotifyChapterReadyForQAAsync(Guid tantouEditorId, Guid chapterId, string chapterTitle, CancellationToken ct = default);
    Task NotifySubmissionReadyForTantouAsync(Guid tantouEditorId, Guid submissionId, string submissionTitle, CancellationToken ct = default);

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

    /// <summary>
    /// Thông báo Mangaka rằng chương truyện đã được xuất bản thành công.
    /// Bao gồm URL công khai của chương truyện.
    /// </summary>
    Task NotifyChapterPublishedAsync(
        Guid mangakaId, Guid chapterId, string chapterTitle,
        string publicationUrl, CancellationToken ct = default);

    // ── SUBMISSION WORKFLOW NOTIFICATIONS (Giai đoạn 1) ──────────────────────

    /// <summary>
    /// [Mốc 1] Thông báo toàn bộ EDITORIAL_BOARD khi Mangaka Submit hoặc Re-Submit bản thảo.
    /// Kích hoạt bởi: POST /{id}/submit và POST /{id}/resubmit.
    /// Trạng thái bản thảo chuyển sang Pending_EB_Review.
    /// </summary>
    Task NotifyNewSubmissionToEditorialBoardAsync(
        Guid submissionId, string submissionTitle,
        string authorName, CancellationToken ct = default);

    /// <summary>
    /// [Mốc 2] Thông báo In-app cho các thành viên EB chưa vote trong vòng hiện tại.
    /// Tuyệt đối KHÔNG bắn cho Mangaka để tránh lộ quy trình nội bộ.
    /// Chỉ gửi khi tổng số phiếu hiện tại còn nhỏ hơn ngưỡng cần thiết (3).
    /// </summary>
    Task NotifyVoteCastToRemainingEditorsAsync(
        Guid submissionId, string submissionTitle, string voterName,
        int currentVoteCount, int totalRequired,
        IEnumerable<Guid> remainingEditorIds, CancellationToken ct = default);

    /// <summary>
    /// [Mốc 4] Thông báo Push khẩn cấp cho toàn bộ EDITOR_IN_CHIEF khi xảy ra tranh chấp 1-1-1.
    /// Trạng thái bản thảo chuyển sang Conflict_Escalated.
    /// Tuyệt đối KHÔNG bắn cho Mangaka.
    /// </summary>
    Task NotifyConflictEscalatedToEicAsync(
        Guid submissionId, string submissionTitle,
        string authorName, CancellationToken ct = default);

    /// <summary>
    /// [Mốc 3/5] Thông báo cho Tantou Editor vừa được gán phụ trách tác phẩm mới sau khi Approve.
    /// Gửi sau khi MangaSeries được tạo và load-balancing đã chọn xong TE.
    /// </summary>
    Task NotifyTantouEditorAssignedAsync(
        Guid tantouEditorId, Guid submissionId, string seriesTitle,
        string authorName, CancellationToken ct = default);

    /// <summary>
    /// Thông báo cho Assistant khi được giao một Segmentation Task mới.
    /// Gửi cả in-app notification và push SignalR realtime.
    /// </summary>
    Task NotifySegmentationTaskAssignedAsync(
        Guid assistantId, Guid segmentationTaskId, string taskType,
        CancellationToken ct = default);

    Task NotifyTaskDeadline3DaysAsync(
        Guid assistantId, Guid pageTaskId, int pageNumber, DateTime deadline,
        CancellationToken ct = default);

    Task NotifyTaskOverdueWarningAsync(
        Guid assistantId, Guid pageTaskId, int pageNumber,
        CancellationToken ct = default);

    Task NotifyAssistantPenalizedAsync(
        Guid assistantId, int warningCount,
        CancellationToken ct = default);

    Task NotifyDeadlineExtensionRequestedAsync(
        Guid mangakaId, Guid requestId, Guid pageTaskId, int pageNumber, DateTime requestedDeadline,
        CancellationToken ct = default);

    Task NotifyExtensionRequestHandledAsync(
        Guid assistantId, Guid pageTaskId, int pageNumber, bool isApproved, string? rejectionReason, DateTime? newDeadline,
        CancellationToken ct = default);
}
