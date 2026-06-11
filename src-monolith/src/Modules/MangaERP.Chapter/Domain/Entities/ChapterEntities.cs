using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Chapter.Domain.Entities;

public enum ChapterStatus { Draft, ReadyForQA, Rejected, Approved, Published, Archived }
public enum PageTaskStatus { Pending, Incomplete, Reviewing, Approved }

public class Chapter : AggregateRoot, ISoftDeletable
{
    public Guid SeriesId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal ChapterNumber { get; private set; }
    public int TotalPages { get; private set; }
    public ChapterStatus Status { get; private set; } = ChapterStatus.Draft;
    public string? IssueType { get; private set; }
    public Guid? AssignedEditorId { get; private set; }
    public DateTime? ScheduledPublishAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public virtual ICollection<PageTask> PageTasks { get; private set; } = new List<PageTask>();

    private Chapter() { }

    public static Chapter Create(Guid seriesId, string title, decimal chapterNumber,
        int totalPages, Guid? assignedEditorId = null)
    {
        if (totalPages <= 0) throw new ArgumentException("TotalPages must be > 0.");
        return new Chapter { SeriesId = seriesId, Title = title, ChapterNumber = chapterNumber,
            TotalPages = totalPages, AssignedEditorId = assignedEditorId,
            Status = ChapterStatus.Draft, CreatedAt = DateTime.UtcNow };
    }

    public void SubmitForQA()
    {
        if (Status != ChapterStatus.Draft && Status != ChapterStatus.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected chapters can be submitted for QA.");
        Status = ChapterStatus.ReadyForQA;
    }

    public void Approve() => Status = ChapterStatus.Approved;
    public void Reject() => Status = ChapterStatus.Rejected;
    public void Archive() => Status = ChapterStatus.Archived;

    public void Publish(string? issueType = null)
    {
        Status = ChapterStatus.Published;
        PublishedAt = DateTime.UtcNow;
        IssueType = issueType ?? IssueType;
    }

    public void SetPublishSchedule(string issueType, DateTime scheduledPublishAt)
    {
        IssueType = issueType;
        ScheduledPublishAt = scheduledPublishAt;
    }
}

public class PageTask : AggregateRoot, ISoftDeletable
{
    public Guid ChapterId { get; set; }
    public int PageNumber { get; set; }
    public Guid? AssignedAssistantId { get; set; }
    public PageTaskStatus TaskStatus { get; set; } = PageTaskStatus.Pending;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public virtual Chapter Chapter { get; set; } = null!;
    public virtual PreviewPage? PreviewPage { get; set; }
}

public class PreviewPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageTaskId { get; set; }
    public string CompositeFileUrl { get; set; } = string.Empty;
    public string? ProductionFileUrl { get; set; }
    public bool IsPublished { get; set; } = false;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public virtual PageTask PageTask { get; set; } = null!;
}
