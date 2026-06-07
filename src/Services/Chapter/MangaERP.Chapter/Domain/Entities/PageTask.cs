using MangaERP.BuildingBlocks.Infrastructure.Persistence;

namespace MangaERP.Chapter.Domain.Entities;

public enum PageTaskStatus { Pending, Incomplete, Reviewing, Approved }

/// <summary>
/// Per-page task tracking. [FIX-7 CRITICAL]: Added AssignedAssistantId, CreatedAt.
/// </summary>
public class PageTask : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public int PageNumber { get; set; }

    // [FIX-7] CRITICAL — assistant task list screen requires this
    public Guid? AssignedAssistantId { get; set; }
    public PageTaskStatus TaskStatus { get; set; } = PageTaskStatus.Pending;

    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Chapter? Chapter { get; set; }
    public virtual PreviewPage? PreviewPage { get; set; }
}
