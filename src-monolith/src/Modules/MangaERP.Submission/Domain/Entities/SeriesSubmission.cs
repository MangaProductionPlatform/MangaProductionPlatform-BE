using MangaERP.Shared.Domain.Abstractions;
using MangaERP.Submission.Domain.Exceptions;

namespace MangaERP.Submission.Domain.Entities;

public enum SubmissionStatus
{
    Draft,
    Pending_TE_Review,
    Pending_EB_Review,
    Requires_Revision,
    TE_Rejected,
    EB_Rejected,
    EB_Approved
}

public class SeriesSubmission : AggregateRoot, ISoftDeletable
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Genre { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string? ManuscriptUrl { get; private set; }
    public Guid SubmitterId { get; private set; }
    public SubmissionStatus Status { get; private set; } = SubmissionStatus.Draft;
    public string? FeedbackMessage { get; private set; }
    public Guid? AssignedEditorId { get; private set; }
    public string? EditorRecommendationMessage { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private SeriesSubmission() { }

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Mangaka tạo draft mới. ManuscriptUrl có thể null lúc đầu,
    /// bắt buộc phải có trước khi gọi SubmitDraft().
    /// </summary>
    public static SeriesSubmission CreateDraft(
        Guid submitterId, string title, string? description,
        string? genre, string? coverImageUrl, string? manuscriptUrl = null)
        => new()
        {
            SubmitterId = submitterId,
            Title = title,
            Description = description,
            Genre = genre,
            CoverImageUrl = coverImageUrl,
            ManuscriptUrl = manuscriptUrl,
            Status = SubmissionStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

    // ── Mangaka transitions ────────────────────────────────────────────────────

    /// <summary>
    /// Nộp draft lần đầu: Draft → Pending_TE_Review.
    /// ManuscriptUrl phải có trước khi gọi.
    /// </summary>
    public void SubmitDraft()
    {
        if (Status != SubmissionStatus.Draft)
            throw new InvalidStateTransitionException("Chỉ có bản thảo ở trạng thái Draft mới được nộp lần đầu.");
        if (string.IsNullOrWhiteSpace(ManuscriptUrl))
            throw new InvalidStateTransitionException("Vui lòng tải lên file bản thảo trước khi nộp.");
        Status = SubmissionStatus.Pending_TE_Review;
    }

    /// <summary>
    /// Nộp lại sau khi chỉnh sửa: Requires_Revision → Pending_TE_Review.
    /// </summary>
    public void ReSubmit()
    {
        if (Status != SubmissionStatus.Requires_Revision)
            throw new InvalidStateTransitionException("Chỉ có thể nộp lại bản thảo khi trạng thái là Requires_Revision.");
        Status = SubmissionStatus.Pending_TE_Review;
        FeedbackMessage = null;
    }

    /// <summary>
    /// Cập nhật manuscript URL (Draft hoặc Requires_Revision).
    /// </summary>
    public void UpdateManuscript(string newManuscriptUrl)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.Requires_Revision)
            throw new InvalidStateTransitionException("Không thể chỉnh sửa bản thảo khi đang trong quá trình xét duyệt hoặc đã đóng băng.");
        ManuscriptUrl = newManuscriptUrl;
    }

    /// <summary>
    /// Cập nhật metadata draft (Draft hoặc Requires_Revision).
    /// </summary>
    public void UpdateDraftMetadata(string title, string? description, string? genre, string? coverImageUrl)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.Requires_Revision)
            throw new InvalidStateTransitionException("Không thể chỉnh sửa bản thảo khi đang trong quá trình xét duyệt hoặc đã đóng băng.");
        Title = title;
        Description = description;
        Genre = genre;
        CoverImageUrl = coverImageUrl;
    }

    // ── Tantou Editor transitions ──────────────────────────────────────────────

    /// <summary>
    /// Editor bắt đầu xét: Pending_TE_Review (Trạng thái không đổi, chỉ gán AssignedEditorId).
    /// </summary>
    public void StartReview(Guid editorId)
    {
        if (Status != SubmissionStatus.Pending_TE_Review)
            throw new InvalidStateTransitionException("Chỉ có thể nhận kiểm duyệt khi bản thảo ở trạng thái Pending_TE_Review.");
        AssignedEditorId = editorId;
    }

    /// <summary>
    /// Editor recommend lên Board: Pending_TE_Review → Pending_EB_Review.
    /// </summary>
    public void RecommendToBoard(Guid editorId, string message)
    {
        if (Status != SubmissionStatus.Pending_TE_Review)
            throw new InvalidStateTransitionException("Biên tập viên chỉ được phép đề xuất khi bản thảo đang chờ TE duyệt.");
        Status = SubmissionStatus.Pending_EB_Review;
        AssignedEditorId = editorId;
        EditorRecommendationMessage = message;
        ReviewedByUserId = editorId;
        ReviewedAt = DateTime.UtcNow;
    }

    // ── Editorial Board transitions ────────────────────────────────────────────

    /// <summary>
    /// Board duyệt: Pending_EB_Review → EB_Approved.
    /// </summary>
    public void Approve(Guid reviewerId)
    {
        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Chỉ có thể duyệt bản thảo khi đã được chuyển tiếp lên Ban Biên Tập.");
        Status = SubmissionStatus.EB_Approved;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    // ── Shared transitions (Editor hoặc Board) ────────────────────────────────

    /// <summary>
    /// Từ chối (TE hoặc EB).
    /// </summary>
    public void Reject(string actorRole, Guid reviewerId, string feedbackMessage)
    {
        if (string.IsNullOrWhiteSpace(feedbackMessage))
            throw new InvalidStateTransitionException("Lý do từ chối không được để trống.");

        if (actorRole == "TantouEditor")
        {
            if (Status != SubmissionStatus.Pending_TE_Review)
                throw new InvalidStateTransitionException("Tantou Editor chỉ được từ chối khi bản thảo đang chờ TE duyệt.");
            Status = SubmissionStatus.TE_Rejected;
        }
        else if (actorRole == "EditorialBoard")
        {
            if (Status != SubmissionStatus.Pending_EB_Review)
                throw new InvalidStateTransitionException("Editorial Board chỉ được từ chối khi bản thảo đang ở bước EB duyệt.");
            Status = SubmissionStatus.EB_Rejected;
        }
        else
        {
            throw new InvalidStateTransitionException("Vai trò này không có quyền từ chối bản thảo.");
        }

        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Yêu cầu chỉnh sửa (TE hoặc EB).
    /// </summary>
    public void RequestRevision(string actorRole, Guid reviewerId, string feedbackMessage)
    {
        if (string.IsNullOrWhiteSpace(feedbackMessage))
            throw new InvalidStateTransitionException("Lý do yêu cầu sửa đổi không được để trống.");

        if (actorRole == "TantouEditor")
        {
            if (Status != SubmissionStatus.Pending_TE_Review)
                throw new InvalidStateTransitionException("Tantou Editor chỉ được yêu cầu sửa đổi khi bản thảo đang chờ TE duyệt.");
        }
        else if (actorRole == "EditorialBoard")
        {
            if (Status != SubmissionStatus.Pending_EB_Review)
                throw new InvalidStateTransitionException("Editorial Board chỉ được yêu cầu sửa đổi khi bản thảo đang ở bước EB duyệt.");
        }
        else
        {
            throw new InvalidStateTransitionException("Vai trò này không có quyền yêu cầu sửa đổi bản thảo.");
        }

        Status = SubmissionStatus.Requires_Revision;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }
}
