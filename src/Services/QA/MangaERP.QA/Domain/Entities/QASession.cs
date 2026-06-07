namespace MangaERP.QA.Domain.Entities;

/// <summary>
/// QA session for a chapter. Auto-created when Chapter.Status = ReadyForQA.
/// </summary>
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

    public virtual ICollection<BugPin> BugPins { get; set; } = new List<BugPin>();
}

