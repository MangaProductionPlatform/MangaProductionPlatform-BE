using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.QA.Domain.Entities;

public class BugPin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Guid PageTaskId { get; set; }
    public Guid EditorId { get; set; }
    public decimal CoordinateX { get; set; }
    public decimal CoordinateY { get; set; }
    public string NoteMessage { get; set; } = string.Empty;
    public string? IssueType { get; set; }  // Visual | Content | Text | Layout
    public string Severity { get; set; } = "Medium";  // Low | Medium | High | Critical
    public string? Category { get; set; }  // Art | Dialogue | Lettering | Composition | Continuity
    public Guid BatchToken { get; set; }
    public string Status { get; set; } = "Open";  // Open | InFixing | Fixed | Resolved
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class QASession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Guid EditorId { get; set; }
    public string Status { get; set; } = "InProgress";  // InProgress | Completed
    public bool IsApproved { get; set; } = false;
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
