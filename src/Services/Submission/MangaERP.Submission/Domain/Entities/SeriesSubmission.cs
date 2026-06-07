using MangaERP.BuildingBlocks.Domain.Abstractions;
using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.Submission.Domain.Enums;
using MangaERP.Submission.Domain.Events;

namespace MangaERP.Submission.Domain.Entities;

/// <summary>
/// Aggregate root for MF1 series proposal. Includes FIX-2: ReviewedByUserId, ReviewedAt.
/// </summary>
public class SeriesSubmission : AggregateRoot, ISoftDeletable
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Genre { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string ManuscriptUrl { get; private set; } = string.Empty;
    public Guid SubmitterId { get; private set; }
    public SubmissionStatus Status { get; private set; } = SubmissionStatus.Pending;
    public string? FeedbackMessage { get; private set; }

    public Guid? AssignedEditorId { get; private set; }
    public string? EditorRecommendationMessage { get; private set; }

    // [FIX-2] Audit: who reviewed and when
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    // Soft Delete
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private SeriesSubmission() { }

    public static SeriesSubmission Create(Guid submitterId, string title, string? description, string? genre, string? coverImageUrl, string manuscriptUrl)
    {
        return new SeriesSubmission
        {
            SubmitterId = submitterId,
            Title = title,
            Description = description,
            Genre = genre,
            CoverImageUrl = coverImageUrl,
            ManuscriptUrl = manuscriptUrl,
            Status = SubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Submit()
    {
        if (Status != SubmissionStatus.Pending && Status != SubmissionStatus.RevisionRequired)
             throw new InvalidOperationException("Only Pending or RevisionRequired submissions can be submitted.");
        Status = SubmissionStatus.UnderReview;
    }

    public void RecommendToBoard(Guid editorId, string message)
    {
        if (Status != SubmissionStatus.Pending && Status != SubmissionStatus.UnderReview)
            throw new InvalidOperationException("Can only recommend submissions that are Pending or UnderReview.");
        Status = SubmissionStatus.RecommendedToBoard;
        AssignedEditorId = editorId;
        EditorRecommendationMessage = message;
        ReviewedByUserId = editorId;
        ReviewedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewerId)
    {
        if (Status != SubmissionStatus.RecommendedToBoard)
            throw new InvalidOperationException("Can only approve submissions that have been recommended to the board.");
        Status = SubmissionStatus.Approved;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        RaiseDomainEvent(new SubmissionApproved(Guid.NewGuid(), DateTime.UtcNow, Id, SubmitterId));
    }

    public void Reject(Guid reviewerId, string feedbackMessage)
    {
        Status = SubmissionStatus.Rejected;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
        RaiseDomainEvent(new SubmissionRejected(Guid.NewGuid(), DateTime.UtcNow, Id, SubmitterId));
    }

    public void RequestRevision(Guid reviewerId, string feedbackMessage)
    {
        Status = SubmissionStatus.RevisionRequired;
        FeedbackMessage = feedbackMessage;
        ReviewedByUserId = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }

    public void UpdateManuscript(string newManuscriptUrl)
    {
        if (Status != SubmissionStatus.RevisionRequired)
            throw new InvalidOperationException("Can only re-upload manuscript when revision is required.");
        ManuscriptUrl = newManuscriptUrl;
    }
}
