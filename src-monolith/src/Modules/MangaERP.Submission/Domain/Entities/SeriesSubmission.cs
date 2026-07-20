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
    /// ManuscriptUrl phải có trước khi gọi.
    /// </summary>
    public void SubmitDraft(Guid tantouEditorId)
    {
        if (Status != SubmissionStatus.Draft)
            throw new InvalidStateTransitionException("Chỉ có bản thảo ở trạng thái Draft mới được nộp lần đầu.");
        if (string.IsNullOrWhiteSpace(ManuscriptUrl))
            throw new InvalidStateTransitionException("Vui lòng tải lên file bản thảo trước khi nộp.");
        if (tantouEditorId == Guid.Empty)
            throw new InvalidStateTransitionException("An assigned Tantou Editor is required.");
        AssignedEditorId = tantouEditorId;
        Status = SubmissionStatus.Pending_Tantou_Review;
    }

    /// <summary>
    /// Nộp lại sau khi chỉnh sửa: Requires_Revision → Pending_EB_Review.
    /// </summary>
    public void ReSubmit()
    {
        if (Status != SubmissionStatus.Requires_Revision &&
            Status != SubmissionStatus.Mangaka_Revision_Required &&
            Status != SubmissionStatus.Tantou_Revision_Required)
            throw new InvalidStateTransitionException("Chỉ có thể nộp lại bản thảo khi trạng thái là Requires_Revision.");
        Status = SubmissionStatus.Pending_Tantou_Review;
        FeedbackMessage = null;
        TantouGuidance = null;
    }

    /// <summary>
    /// Cập nhật manuscript URL (Draft hoặc Requires_Revision).
    /// </summary>
    public void UpdateManuscript(string newManuscriptUrl)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.Requires_Revision &&
            Status != SubmissionStatus.Mangaka_Revision_Required && Status != SubmissionStatus.Tantou_Revision_Required)
            throw new InvalidStateTransitionException("Không thể chỉnh sửa bản thảo khi đang trong quá trình xét duyệt hoặc đã đóng băng.");
        ManuscriptUrl = newManuscriptUrl;
    }

    /// <summary>
    /// Cập nhật metadata draft (Draft hoặc Requires_Revision).
    /// </summary>
    public void UpdateDraftMetadata(string title, string? description, string? genre, string? coverImageUrl)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.Requires_Revision &&
            Status != SubmissionStatus.Mangaka_Revision_Required && Status != SubmissionStatus.Tantou_Revision_Required)
            throw new InvalidStateTransitionException("Không thể chỉnh sửa bản thảo khi đang trong quá trình xét duyệt hoặc đã đóng băng.");
        Title = title;
        Description = description;
        Genre = genre;
        CoverImageUrl = coverImageUrl;
    }

    // ── Editorial Board transitions ────────────────────────────────────────────

    /// <summary>
    /// Board duyệt: Pending_EB_Review → EB_Approved.
    /// </summary>
    public void ReturnByTantou(Guid tantouId, string guidance)
    {
        EnsureAssignedTantou(tantouId);
        if (Status != SubmissionStatus.Pending_Tantou_Review)
            throw new InvalidStateTransitionException("Only work awaiting Tantou review can be returned.");
        if (string.IsNullOrWhiteSpace(guidance))
            throw new InvalidStateTransitionException("Revision guidance is required.");
        TantouGuidance = guidance.Trim();
        TantouReviewedAt = DateTime.UtcNow;
        Status = SubmissionStatus.Tantou_Revision_Required;
    }

    public void RecommendToEditorialBoard(Guid tantouId)
    {
        EnsureAssignedTantou(tantouId);
        if (Status != SubmissionStatus.Pending_Tantou_Review)
            throw new InvalidStateTransitionException("Only work awaiting Tantou review can be recommended.");
        Status = SubmissionStatus.Pending_EB_Review;
        TantouReviewedAt = DateTime.UtcNow;
        RecommendedAt = DateTime.UtcNow;
    }

    public void RejectToTantou(Guid reviewerId, string feedback)
    {
        if (Status != SubmissionStatus.Pending_EB_Review && Status != SubmissionStatus.Conflict_Escalated)
            throw new InvalidStateTransitionException("This work is not awaiting an editorial decision.");
        if (string.IsNullOrWhiteSpace(feedback))
            throw new InvalidStateTransitionException("Rejection feedback is required.");
        Status = SubmissionStatus.Editorial_Rejected_To_Tantou;
        FeedbackMessage = feedback.Trim();
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    public void ReturnConsolidatedGuidanceToMangaka(Guid tantouId, string guidance)
    {
        EnsureAssignedTantou(tantouId);
        if (Status != SubmissionStatus.Editorial_Rejected_To_Tantou)
            throw new InvalidStateTransitionException("Only rejected work can be returned to the Mangaka.");
        if (string.IsNullOrWhiteSpace(guidance))
            throw new InvalidStateTransitionException("Consolidated revision guidance is required.");
        TantouGuidance = guidance.Trim();
        TantouReviewedAt = DateTime.UtcNow;
        Status = SubmissionStatus.Mangaka_Revision_Required;
        CurrentRound++;
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
        if (string.IsNullOrWhiteSpace(feedbackMessage))
            throw new InvalidStateTransitionException("Lý do yêu cầu sửa đổi không được để trống.");

        if (actorRole != "EditorialBoard")
            throw new InvalidStateTransitionException("Chỉ có Editorial Board mới có quyền yêu cầu sửa đổi bản thảo.");

        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Editorial Board chỉ được yêu cầu sửa đổi khi bản thảo đang chờ duyệt.");

        Status = SubmissionStatus.Requires_Revision;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        CurrentRound++; // Increment round to allow fresh voting on resubmission
    }


    // ── Collective voting transitions ─────────────────────────────────────────

    /// <summary>
    /// Chuyển trạng thái sang Conflict_Escalated khi 3 phiếu bầu cho 3 quyết định khác nhau.
    /// </summary>
    public void EscalateConflict()
    {
        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Chỉ có thể leo thang tranh chấp khi bản thảo đang chờ duyệt.");
        Status = SubmissionStatus.Conflict_Escalated;
        ReviewedAt = DateTime.UtcNow;
    }

    // ── Editor-in-Chief arbitration ───────────────────────────────────────────

    /// <summary>
    /// EIC phê duyệt bản thảo đang tranh chấp: Conflict_Escalated → EB_Approved.
    /// </summary>
    public void ApproveByEIC(Guid eicId)
    {
        if (Status != SubmissionStatus.Conflict_Escalated)
            throw new InvalidStateTransitionException("Chỉ có thể phê duyệt khi bản thảo đang ở trạng thái Conflict_Escalated.");
        Status = SubmissionStatus.EB_Approved;
        ReviewedByUserId = eicId;
        ReviewedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// EIC từ chối bản thảo đang tranh chấp: Conflict_Escalated → EB_Rejected.
    /// </summary>
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

    /// <summary>
    /// EIC yêu cầu chỉnh sửa: Conflict_Escalated → Requires_Revision.
    /// Tăng CurrentRound +1 để mở khóa cho vòng bỏ phiếu mới.
    /// </summary>
    public void RequestRevisionByEIC(Guid eicId, string feedbackMessage)
    {
        if (Status != SubmissionStatus.Conflict_Escalated)
            throw new InvalidStateTransitionException("Chỉ có thể yêu cầu sửa đổi khi bản thảo đang ở trạng thái Conflict_Escalated.");
        if (string.IsNullOrWhiteSpace(feedbackMessage))
            throw new InvalidStateTransitionException("Lý do yêu cầu sửa đổi không được để trống.");
        Status = SubmissionStatus.Requires_Revision;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = eicId;
        ReviewedAt = DateTime.UtcNow;
        CurrentRound++;  // Unlock new voting round
    }
}
