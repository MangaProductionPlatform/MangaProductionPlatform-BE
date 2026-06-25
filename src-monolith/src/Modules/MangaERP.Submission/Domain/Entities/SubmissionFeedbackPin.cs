namespace MangaERP.Submission.Domain.Entities;

public enum FeedbackPinCategory { Visual, Content, Typo }

public class SubmissionFeedbackPin
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SubmissionId { get; private set; }
    public string PageIdentifier { get; private set; } = string.Empty; // Page number or Image URL
    public double CoordinateX { get; private set; } // 0-100% relative coordinate
    public double CoordinateY { get; private set; } // 0-100% relative coordinate
    public string Comment { get; private set; } = string.Empty;
    public FeedbackPinCategory Category { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public bool IsArchived { get; private set; } = false;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private SubmissionFeedbackPin() { }

    public static SubmissionFeedbackPin Create(
        Guid submissionId, string pageIdentifier, double x, double y,
        string comment, FeedbackPinCategory category, Guid createdByUserId)
    {
        if (x < 0 || x > 100 || y < 0 || y > 100)
            throw new ArgumentException("Coordinates must be between 0 and 100 percent.");
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Comment cannot be empty.");

        return new SubmissionFeedbackPin
        {
            SubmissionId = submissionId,
            PageIdentifier = pageIdentifier,
            CoordinateX = x,
            CoordinateY = y,
            Comment = comment,
            Category = category,
            CreatedByUserId = createdByUserId
        };
    }

    public void Archive() => IsArchived = true;
}
