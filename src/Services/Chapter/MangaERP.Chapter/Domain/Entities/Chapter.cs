using MangaERP.BuildingBlocks.Domain.Abstractions;
using MangaERP.BuildingBlocks.Infrastructure.Persistence;

namespace MangaERP.Chapter.Domain.Entities;

public enum ChapterStatus { Draft, ReadyForQA, Rejected, Approved, Published, Archived }

/// <summary>
/// Chapter aggregate root. [FIX-4]: Added AssignedEditorId, PublishedAt.
/// </summary>
public class Chapter : AggregateRoot, ISoftDeletable
{
    public Guid SeriesId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal ChapterNumber { get; private set; }
    public int TotalPages { get; private set; }
    public ChapterStatus Status { get; private set; } = ChapterStatus.Draft;
    public string? IssueType { get; private set; }  // Weekly | Monthly | Special

    // [FIX-4]
    public Guid? AssignedEditorId { get; private set; }
    public DateTime? ScheduledPublishAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    // Soft Delete
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<PageTask> PageTasks { get; private set; } = new List<PageTask>();

    private Chapter() { }

    public static Chapter Create(Guid seriesId, string title, decimal chapterNumber, int totalPages, Guid? assignedEditorId = null)
    {
        if (totalPages <= 0) throw new ArgumentException("TotalPages must be > 0.");
        return new Chapter
        {
            SeriesId = seriesId,
            Title = title,
            ChapterNumber = chapterNumber,
            TotalPages = totalPages,
            AssignedEditorId = assignedEditorId,
            Status = ChapterStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SubmitForQA()
    {
        if (Status != ChapterStatus.Draft && Status != ChapterStatus.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected chapters can be submitted for QA.");
        Status = ChapterStatus.ReadyForQA;
        RaiseDomainEvent(new ChapterSubmittedForQA(Guid.NewGuid(), DateTime.UtcNow, Id, SeriesId));
    }

    public void Approve()
    {
        Status = ChapterStatus.Approved;
        RaiseDomainEvent(new ChapterApproved(Guid.NewGuid(), DateTime.UtcNow, Id, SeriesId));
    }

    public void Reject() => Status = ChapterStatus.Rejected;

    public void Publish(string? issueType = null)
    {
        Status = ChapterStatus.Published;
        PublishedAt = DateTime.UtcNow;
        IssueType = issueType ?? IssueType;
    }

    public void Archive() => Status = ChapterStatus.Archived;

    public void SetPublishSchedule(string issueType, DateTime scheduledPublishAt)
    {
        IssueType = issueType;
        ScheduledPublishAt = scheduledPublishAt;
    }
}
