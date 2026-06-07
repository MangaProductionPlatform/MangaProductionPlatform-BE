namespace MangaERP.QA.Domain.Entities;

public enum IssueType { Visual, Content, Text, Layout }
public enum BugStatus { Open, InFixing, Resolved }

/// <summary>
/// Bug pin anchored by percent-based coordinates.
/// [FIX-10 CRITICAL]: Added ChapterId (denormalized), IssueType, ResolvedAt.
/// </summary>
public class BugPin
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // [FIX-10] CRITICAL — enables fast chapter-level queries without double JOIN
    public Guid ChapterId { get; set; }
    public Guid PageTaskId { get; set; }
    public Guid EditorId { get; set; }

    /// <summary>X coordinate as % (0.00–100.00)</summary>
    public decimal CoordinateX { get; set; }
    /// <summary>Y coordinate as % (0.00–100.00)</summary>
    public decimal CoordinateY { get; set; }

    public string NoteMessage { get; set; } = string.Empty;

    // [FIX-10] IssueType classification
    public IssueType? IssueType { get; set; }

    public Guid BatchToken { get; set; }
    public BugStatus Status { get; set; } = BugStatus.Open;

    // [FIX-10] ResolvedAt timestamp
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public void Resolve()
    {
        Status = BugStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }

    public void MarkInFixing() => Status = BugStatus.InFixing;
}
