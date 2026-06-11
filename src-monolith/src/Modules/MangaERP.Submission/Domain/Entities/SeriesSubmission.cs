using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Submission.Domain.Entities;

public enum SubmissionStatus
{
    Draft,                // Mangaka đang soạn, chưa nộp
    Pending,              // Đã nộp, chờ Editor xét
    UnderReview,          // Editor đang xét
    RecommendedToBoard,   // Editor recommend lên Board
    RevisionRequired,     // Cần chỉnh sửa (từ Editor hoặc Board)
    Approved,             // Board duyệt → tạo MangaSeries
    Rejected              // Bị từ chối
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
    /// Nộp draft lần đầu: Draft → Pending.
    /// ManuscriptUrl phải có trước khi gọi.
    /// </summary>
    public void SubmitDraft()
    {
        if (Status != SubmissionStatus.Draft)
            throw new InvalidOperationException("Only Draft submissions can be submitted for the first time.");
        if (string.IsNullOrWhiteSpace(ManuscriptUrl))
            throw new InvalidOperationException("ManuscriptUrl is required before submitting.");
        Status = SubmissionStatus.Pending;
    }

    /// <summary>
    /// Nộp lại sau khi chỉnh sửa: RevisionRequired → Pending.
    /// </summary>
    public void ReSubmit()
    {
        if (Status != SubmissionStatus.RevisionRequired)
            throw new InvalidOperationException("Can only re-submit when revision is required.");
        Status = SubmissionStatus.Pending;
        FeedbackMessage = null;
    }

    /// <summary>
    /// Cập nhật manuscript URL (Draft hoặc RevisionRequired).
    /// </summary>
    public void UpdateManuscript(string newManuscriptUrl)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.RevisionRequired)
            throw new InvalidOperationException("Can only update manuscript when Draft or RevisionRequired.");
        ManuscriptUrl = newManuscriptUrl;
    }

    /// <summary>
    /// Cập nhật metadata draft (Draft hoặc RevisionRequired).
    /// </summary>
    public void UpdateDraftMetadata(string title, string? description, string? genre, string? coverImageUrl)
    {
        if (Status != SubmissionStatus.Draft && Status != SubmissionStatus.RevisionRequired)
            throw new InvalidOperationException("Can only update metadata when Draft or RevisionRequired.");
        Title = title;
        Description = description;
        Genre = genre;
        CoverImageUrl = coverImageUrl;
    }

    // ── Tantou Editor transitions ──────────────────────────────────────────────

    /// <summary>
    /// Editor bắt đầu xét: Pending → UnderReview.
    /// </summary>
    public void StartReview(Guid editorId)
    {
        if (Status != SubmissionStatus.Pending)
            throw new InvalidOperationException("Can only start review on Pending submissions.");
        Status = SubmissionStatus.UnderReview;
        AssignedEditorId = editorId;
    }

    /// <summary>
    /// Editor recommend lên Board: Pending/UnderReview → RecommendedToBoard.
    /// </summary>
    public void RecommendToBoard(Guid editorId, string message)
    {
        if (Status != SubmissionStatus.UnderReview && Status != SubmissionStatus.Pending)
            throw new InvalidOperationException("Can only recommend Pending or UnderReview submissions.");
        Status = SubmissionStatus.RecommendedToBoard;
        AssignedEditorId = editorId;
        EditorRecommendationMessage = message;
        ReviewedByUserId = editorId;
        ReviewedAt = DateTime.UtcNow;
    }

    // ── Editorial Board transitions ────────────────────────────────────────────

    /// <summary>
    /// Board duyệt: RecommendedToBoard → Approved.
    /// Handler phải tạo MangaSeries trong cùng transaction.
    /// </summary>
    public void Approve(Guid reviewerId)
    {
        if (Status != SubmissionStatus.RecommendedToBoard)
            throw new InvalidOperationException("Can only approve submissions recommended to the board.");
        Status = SubmissionStatus.Approved;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    // ── Shared transitions (Editor hoặc Board) ────────────────────────────────

    /// <summary>
    /// Từ chối (Editor hoặc Board).
    /// </summary>
    public void Reject(Guid reviewerId, string feedbackMessage)
    {
        if (Status == SubmissionStatus.Approved || Status == SubmissionStatus.Draft)
            throw new InvalidOperationException("Cannot reject an Approved or Draft submission.");
        Status = SubmissionStatus.Rejected;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Yêu cầu chỉnh sửa (Editor hoặc Board).
    /// </summary>
    public void RequestRevision(Guid reviewerId, string feedbackMessage)
    {
        if (Status == SubmissionStatus.Approved || Status == SubmissionStatus.Draft || Status == SubmissionStatus.Rejected)
            throw new InvalidOperationException("Cannot request revision on Approved, Draft, or Rejected submissions.");
        Status = SubmissionStatus.RevisionRequired;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }
}
