using MangaERP.Shared.Domain.Abstractions;
using MangaERP.Submission.Domain.Exceptions;

namespace MangaERP.Submission.Domain.Entities;

public enum SubmissionStatus
{
    Draft,
    Pending_Tantou_Review,
    Tantou_Revision_Required,
    Pending_EB_Review,
    Requires_Revision,
    Editorial_Rejected_To_Tantou,
    Mangaka_Revision_Required,
    EB_Rejected,
    EB_Approved,
    Conflict_Escalated   // 1-1-1 vote deadlock — awaiting Editor-in-Chief arbitration
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
    public string? TantouGuidance { get; private set; }
    public DateTime? TantouReviewedAt { get; private set; }
    public DateTime? RecommendedAt { get; private set; }

    /// <summary>
    /// Voting round number — starts at 1, incremented by +1 each time REQ_REVISION
    /// is issued by the Editor-in-Chief so the board can vote fresh on the resubmission.
    /// </summary>
    public int CurrentRound { get; private set; } = 1;

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
            CurrentRound = 1,
            CreatedAt = DateTime.UtcNow
        };

    // ── Mangaka transitions ────────────────────────────────────────────────────

    /// <summary>
    /// Nộp draft lần đầu: Draft → Pending_EB_Review.
    /// ManuscriptUrl phải có trước khi gọi. Nộp trực tiếp cho Editorial Board, Tantou Editor không tham gia MF1.
    /// </summary>
    public void SubmitDraft()
    {
        if (Status != SubmissionStatus.Draft)
            throw new InvalidStateTransitionException("Chỉ có bản thảo ở trạng thái Draft mới được nộp lần đầu.");
        if (string.IsNullOrWhiteSpace(ManuscriptUrl))
            throw new InvalidStateTransitionException("Vui lòng tải lên file bản thảo trước khi nộp.");

        AssignedEditorId = null;
        Status = SubmissionStatus.Pending_EB_Review;
    }

    /// <summary>
    /// Nộp lại sau khi chỉnh sửa: Requires_Revision / EB_Rejected → Pending_EB_Review trực tiếp.
    /// </summary>
    public void ReSubmit()
    {
        if (Status != SubmissionStatus.Requires_Revision &&
            Status != SubmissionStatus.Mangaka_Revision_Required &&
            Status != SubmissionStatus.Tantou_Revision_Required &&
            Status != SubmissionStatus.Editorial_Rejected_To_Tantou &&
            Status != SubmissionStatus.EB_Rejected)
            throw new InvalidStateTransitionException("Chỉ có thể nộp lại bản thảo khi trạng thái là Requires_Revision hoặc Rejected.");

        Status = SubmissionStatus.Pending_EB_Review;
        FeedbackMessage = null;
    }

    /// <summary>
    /// Cập nhật manuscript URL (Draft hoặc Requires_Revision).
    /// </summary>
    public void UpdateManuscript(string newManuscriptUrl)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.Requires_Revision &&
            Status != SubmissionStatus.Mangaka_Revision_Required && Status != SubmissionStatus.Tantou_Revision_Required &&
            Status != SubmissionStatus.Editorial_Rejected_To_Tantou && Status != SubmissionStatus.EB_Rejected)
            throw new InvalidStateTransitionException("Không thể chỉnh sửa bản thảo khi đang trong quá trình xét duyệt hoặc đã đóng băng.");
        ManuscriptUrl = newManuscriptUrl;
    }

    /// <summary>
    /// Cập nhật metadata draft (Draft hoặc Requires_Revision).
    /// </summary>
    public void UpdateDraftMetadata(string title, string? description, string? genre, string? coverImageUrl)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.Requires_Revision &&
            Status != SubmissionStatus.Mangaka_Revision_Required && Status != SubmissionStatus.Tantou_Revision_Required &&
            Status != SubmissionStatus.Editorial_Rejected_To_Tantou && Status != SubmissionStatus.EB_Rejected)
            throw new InvalidStateTransitionException("Không thể chỉnh sửa bản thảo khi đang trong quá trình xét duyệt hoặc đã đóng băng.");
        Title = title;
        Description = description;
        Genre = genre;
        CoverImageUrl = coverImageUrl;
    }

    // ── Editorial Board transitions ────────────────────────────────────────────

    [Obsolete("Tantou Editor cannot return or gatekeep submissions. Submissions go directly to EB.")]
    public void ReturnByTantou(Guid tantouId, string guidance)
    {
        throw new InvalidOperationException("Tantou Editors cannot approve, return, or gatekeep submissions.");
    }

    [Obsolete("Tantou Editor recommendation is not required. Submissions go directly to EB.")]
    public void RecommendToEditorialBoard(Guid tantouId)
    {
        // No-op or direct transition for backward compatibility
        Status = SubmissionStatus.Pending_EB_Review;
    }

    public void RejectByBoard(Guid reviewerId, string feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback))
            throw new InvalidStateTransitionException("Rejection feedback is required.");
        Status = SubmissionStatus.EB_Rejected;
        FeedbackMessage = feedback.Trim();
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    public void RejectToTantou(Guid reviewerId, string feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback))
            throw new InvalidStateTransitionException("Rejection feedback is required.");
        // Direct feedback to Mangaka via Requires_Revision status
        Status = SubmissionStatus.Requires_Revision;
        FeedbackMessage = feedback.Trim();
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    public void ReturnConsolidatedGuidanceToMangaka(Guid tantouId, string guidance)
    {
        EnsureAssignedTantou(tantouId);
        if (string.IsNullOrWhiteSpace(guidance))
            throw new InvalidStateTransitionException("Consolidated revision guidance is required.");

        // Tantou guidance is non-blocking support note
        TantouGuidance = guidance.Trim();
        TantouReviewedAt = DateTime.UtcNow;
    }

    public void AssignTantou(Guid tantouEditorId)
    {
        if (tantouEditorId == Guid.Empty)
            throw new ArgumentException("Tantou Editor ID cannot be empty.", nameof(tantouEditorId));
        AssignedEditorId = tantouEditorId;
    }

    private void EnsureAssignedTantou(Guid tantouId)
    {
        if (AssignedEditorId != tantouId)
            throw new UnauthorizedAccessException("Only the assigned Tantou Editor can perform this action.");
    }

    public void ApproveByBoard(Guid reviewerId)
    {
        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Chỉ có thể duyệt bản thảo khi đang chờ Ban Biên Tập duyệt.");
        Status = SubmissionStatus.EB_Approved;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    // ── Shared transitions (Board) ────────────────────────────────

    /// <summary>
    /// Từ chối (EB).
    /// </summary>
    [Obsolete("Use the two-reviewer EditorialWorkflowController decision flow.")]
    public void Reject(string actorRole, Guid reviewerId, string feedbackMessage)
    {
        if (string.IsNullOrWhiteSpace(feedbackMessage))
            throw new InvalidStateTransitionException("Lý do từ chối không được để trống.");

        if (actorRole != "EditorialBoard")
            throw new InvalidStateTransitionException("Chỉ có Editorial Board mới có quyền từ chối bản thảo.");

        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Editorial Board chỉ được từ chối khi bản thảo đang chờ duyệt.");

        Status = SubmissionStatus.EB_Rejected;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Yêu cầu chỉnh sửa (EB).
    /// </summary>
    [Obsolete("RequestRevision is not a valid Editorial Board decision.")]
    public void RequestRevision(string actorRole, Guid reviewerId, string feedbackMessage)
    {
        throw new NotSupportedException("RequestRevision is not supported. Submissions must be either Approved or Rejected.");
    }

    // ── Collective voting transitions ─────────────────────────────────────────

    public void EscalateConflict()
    {
        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Chỉ có thể leo thang tranh chấp khi bản thảo đang chờ duyệt.");
        Status = SubmissionStatus.Conflict_Escalated;
        ReviewedAt = DateTime.UtcNow;
    }

    // ── Editor-in-Chief arbitration ───────────────────────────────────────────

    public void ApproveByEIC(Guid eicId)
    {
        if (Status != SubmissionStatus.Conflict_Escalated)
            throw new InvalidStateTransitionException("Chỉ có thể phê duyệt khi bản thảo đang ở trạng thái Conflict_Escalated.");
        Status = SubmissionStatus.EB_Approved;
        ReviewedByUserId = eicId;
        ReviewedAt = DateTime.UtcNow;
    }

    public void RejectByEIC(Guid eicId, string feedbackMessage)
    {
        if (Status != SubmissionStatus.Conflict_Escalated)
            throw new InvalidStateTransitionException("Chỉ có thể từ chối khi bản thảo đang ở trạng thái Conflict_Escalated.");
        if (string.IsNullOrWhiteSpace(feedbackMessage))
            throw new InvalidStateTransitionException("Lý do từ chối không được để trống.");
        Status = SubmissionStatus.EB_Rejected;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = eicId;
        ReviewedAt = DateTime.UtcNow;
    }

    [Obsolete("RequestRevisionByEIC is not a valid EIC arbitration outcome.")]
    public void RequestRevisionByEIC(Guid eicId, string feedbackMessage)
    {
        throw new NotSupportedException("RequestRevision is not supported. Submissions must be either Approved or Rejected.");
    }
}
