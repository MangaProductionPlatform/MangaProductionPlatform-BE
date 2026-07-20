namespace MangaERP.Submission.Domain.Entities;

public enum EditorialWorkType { SeriesSubmission, Chapter }
public enum EditorialReviewAssignmentStatus { Pending, Completed }
public enum EditorialDecision { Approved, Rejected }

/// <summary>One confidential Editorial Board review for a work item and round.</summary>
public class EditorialReviewAssignment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public EditorialWorkType WorkType { get; private set; }
    public Guid WorkId { get; private set; }
    public int RoundNumber { get; private set; }
    public Guid ReviewerId { get; private set; }
    public EditorialReviewAssignmentStatus Status { get; private set; } = EditorialReviewAssignmentStatus.Pending;
    public EditorialDecision? Decision { get; private set; }
    public string? Feedback { get; private set; }
    public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; private set; }
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    private EditorialReviewAssignment() { }

    public static EditorialReviewAssignment Assign(
        EditorialWorkType workType, Guid workId, int roundNumber, Guid reviewerId)
    {
        if (workId == Guid.Empty || reviewerId == Guid.Empty || roundNumber < 1)
            throw new ArgumentException("Work, reviewer, and a positive round are required.");
        return new EditorialReviewAssignment
        {
            WorkType = workType,
            WorkId = workId,
            RoundNumber = roundNumber,
            ReviewerId = reviewerId
        };
    }

    public void Complete(EditorialDecision decision, string? feedback)
    {
        if (!Enum.IsDefined(decision))
            throw new InvalidOperationException("Decision must be Approved or Rejected.");
        if (Status == EditorialReviewAssignmentStatus.Completed)
            throw new InvalidOperationException("This editorial review has already been submitted.");
        if (decision == EditorialDecision.Rejected && string.IsNullOrWhiteSpace(feedback))
            throw new InvalidOperationException("Feedback is required for a rejected review.");
        Decision = decision;
        Feedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        Status = EditorialReviewAssignmentStatus.Completed;
        ReviewedAt = DateTime.UtcNow;
        ConcurrencyToken = Guid.NewGuid();
    }
}
