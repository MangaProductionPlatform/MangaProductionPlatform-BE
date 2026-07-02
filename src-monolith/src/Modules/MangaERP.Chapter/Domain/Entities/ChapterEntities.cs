using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Chapter.Domain.Entities;

public enum ChapterStatus { Draft, ReadyForQA, Rejected, Approved, Published, Archived }
public enum PageTaskStatus { Pending, Incomplete, Reviewing, RevisionAlert, Approved }

/// <summary>Type of artwork work assigned to the assistant for this page region.</summary>
public enum PageTaskType { General, Background, Shading, Inking, Effect, Coloring }

public class Chapter : AggregateRoot, ISoftDeletable
{
    public Guid SeriesId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal ChapterNumber { get; private set; }
    public int TotalPages { get; private set; }
    public ChapterStatus Status { get; private set; } = ChapterStatus.Draft;
    public string? CoverImageUrl { get; private set; }
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
        int totalPages, Guid? assignedEditorId = null, string? coverImageUrl = null)
    {
        if (totalPages <= 0) throw new ArgumentException("TotalPages must be > 0.");
        return new Chapter { SeriesId = seriesId, Title = title, ChapterNumber = chapterNumber,
            TotalPages = totalPages, AssignedEditorId = assignedEditorId,
            CoverImageUrl = coverImageUrl,
            Status = ChapterStatus.Draft, CreatedAt = DateTime.UtcNow };
    }

    public void EnsureOwnedBy(Guid mangakaId, Guid seriesAuthorId)
    {
        if (seriesAuthorId != mangakaId)
            throw new UnauthorizedAccessException("You do not own this chapter's series.");
    }

    public bool CanSubmitForQA()
    {
        var activePages = PageTasks.Where(p => !p.IsDeleted).ToList();
        if (activePages.Count != TotalPages)
            return false;

        return activePages.All(p => p.TaskStatus == PageTaskStatus.Approved);
    }

    public void SubmitForQA()
    {
        if (Status != ChapterStatus.Draft && Status != ChapterStatus.Rejected)
            throw new InvalidOperationException("Only Draft or Rejected chapters can be submitted for QA.");

        if (!CanSubmitForQA())
            throw new InvalidOperationException(
                $"All {TotalPages} pages must be approved before submitting for QA.");

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
    public string? Description { get; set; }
    public PageTaskStatus TaskStatus { get; set; } = PageTaskStatus.Pending;
    /// <summary>SAM-generated mask polygon stored as JSON (array of [x,y] points).</summary>
    public string? RegionMask { get; set; }
    /// <summary>Type of artwork work for this region (Background, Shading, etc.).</summary>
    public PageTaskType TaskType { get; set; } = PageTaskType.General;
    public DateTime? Deadline { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public virtual Chapter Chapter { get; set; } = null!;

    public void SetDeadline(DateTime? deadline)
    {
        Deadline = deadline;
        UpdatedAt = DateTime.UtcNow;
    }
    public virtual PreviewPage? PreviewPage { get; set; }

    public static PageTask CreatePending(Guid chapterId, int pageNumber)
    {
        if (pageNumber <= 0)
            throw new ArgumentException("PageNumber must be > 0.");

        return new PageTask
        {
            ChapterId = chapterId,
            PageNumber = pageNumber,
            TaskStatus = PageTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Activate(Guid assistantId, string? description = null, DateTime? deadline = null)
    {
        if (TaskStatus != PageTaskStatus.Pending)
            throw new InvalidOperationException("Only Pending page tasks can be activated.");

        AssignedAssistantId = assistantId;
        Description = description;
        Deadline = deadline;
        TaskStatus = PageTaskStatus.Incomplete;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reassign(Guid assistantId, string? description = null)
    {
        if (TaskStatus != PageTaskStatus.Incomplete && TaskStatus != PageTaskStatus.RevisionAlert)
            throw new InvalidOperationException("Only Incomplete or RevisionAlert page tasks can be reassigned.");

        AssignedAssistantId = assistantId;
        Description = description ?? Description;
        TaskStatus = PageTaskStatus.Incomplete;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReviewing()
    {
        if (TaskStatus != PageTaskStatus.Incomplete && TaskStatus != PageTaskStatus.RevisionAlert)
            throw new InvalidOperationException("Layer can only be submitted from Incomplete or RevisionAlert status.");

        TaskStatus = PageTaskStatus.Reviewing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Accept()
    {
        if (TaskStatus != PageTaskStatus.Reviewing)
            throw new InvalidOperationException("Only Reviewing page tasks can be accepted.");

        TaskStatus = PageTaskStatus.Approved;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RequestRevision()
    {
        if (TaskStatus != PageTaskStatus.Reviewing)
            throw new InvalidOperationException("Only Reviewing page tasks can be sent for revision.");

        TaskStatus = PageTaskStatus.RevisionAlert;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CanSubmitLayer(Guid assistantId)
    {
        return AssignedAssistantId == assistantId
            && (TaskStatus == PageTaskStatus.Incomplete || TaskStatus == PageTaskStatus.RevisionAlert);
    }

    /// <summary>
    /// Sets the SAM segmentation region and work type for this page task.
    /// Called after Mangaka selects a region on the canvas and assigns a task type.
    /// </summary>
    public void SetRegion(string regionMaskJson, PageTaskType taskType)
    {
        RegionMask = regionMaskJson;
        TaskType   = taskType;
        UpdatedAt  = DateTime.UtcNow;
    }
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

    public static PreviewPage CreateStub(Guid pageTaskId, string compositeFileUrl)
        => new()
        {
            PageTaskId = pageTaskId,
            CompositeFileUrl = compositeFileUrl,
            GeneratedAt = DateTime.UtcNow
        };

    public void UpdateComposite(string compositeFileUrl)
    {
        CompositeFileUrl = compositeFileUrl;
        GeneratedAt = DateTime.UtcNow;
    }
}
