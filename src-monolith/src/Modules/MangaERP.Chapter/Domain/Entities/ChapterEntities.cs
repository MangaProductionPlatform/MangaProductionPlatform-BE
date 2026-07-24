using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Chapter.Domain.Entities;

public enum ChapterStatus
{
    Draft, ReadyForQA, QaRevisionRequired, PendingEditorialReview,
    EditorialRejectedToTantou, MangakaRevisionRequired, ConflictEscalated,
    Approved, Published, Archived
}
public enum PageTaskStatus { Pending, PendingAcceptance, Incomplete, Reviewing, RevisionAlert, Approved, ReassignmentRequired, Cancelled }

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
    public int EditorialRound { get; private set; } = 1;
    public string? EditorialFeedback { get; private set; }
    public string? TantouGuidance { get; private set; }
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

    public void UpdateMetadata(string title, decimal chapterNumber, int totalPages, Guid? assignedEditorId, string? coverImageUrl)
    {
        if (Status != ChapterStatus.Draft && Status != ChapterStatus.QaRevisionRequired &&
            Status != ChapterStatus.MangakaRevisionRequired)
            throw new InvalidOperationException("Only Draft or QaRevisionRequired chapters can have their metadata updated.");

        if (totalPages <= 0)
            throw new ArgumentException("TotalPages must be > 0.");

        Title = title;
        ChapterNumber = chapterNumber;
        TotalPages = totalPages;
        AssignedEditorId = assignedEditorId;
        CoverImageUrl = coverImageUrl;
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
        if (Status != ChapterStatus.Draft && Status != ChapterStatus.QaRevisionRequired &&
            Status != ChapterStatus.MangakaRevisionRequired)
            throw new InvalidOperationException("Only Draft or QaRevisionRequired chapters can be submitted for QA.");

        if (!CanSubmitForQA())
            throw new InvalidOperationException(
                $"All {TotalPages} pages must be approved before submitting for QA.");

        Status = ChapterStatus.PendingEditorialReview;
    }

    [Obsolete("Tantou Editor cannot return or gatekeep chapters.")]
    public void ReturnByTantou(Guid tantouId, string guidance)
    {
        throw new InvalidOperationException("Tantou Editors cannot approve, return, or gatekeep chapters.");
    }

    [Obsolete("Tantou Editor recommendation is not required.")]
    public void RecommendToEditorialBoard(Guid tantouId)
    {
        Status = ChapterStatus.PendingEditorialReview;
    }

    public void RejectToTantou(string feedback)
    {
        if (Status != ChapterStatus.PendingEditorialReview && Status != ChapterStatus.ConflictEscalated)
            throw new InvalidOperationException("This chapter is not awaiting an editorial decision.");
        if (string.IsNullOrWhiteSpace(feedback))
            throw new InvalidOperationException("Rejection feedback is required.");
        EditorialFeedback = feedback.Trim();
        Status = ChapterStatus.QaRevisionRequired;
    }

    public void EscalateEditorialConflict()
    {
        if (Status != ChapterStatus.PendingEditorialReview)
            throw new InvalidOperationException("Only a pending editorial review can be escalated.");
        Status = ChapterStatus.ConflictEscalated;
    }

    public void ReturnConsolidatedGuidanceToMangaka(Guid tantouId, string guidance)
    {
        EnsureAssignedTantou(tantouId);
        if (string.IsNullOrWhiteSpace(guidance))
            throw new InvalidOperationException("Consolidated revision guidance is required.");
        TantouGuidance = guidance.Trim();
    }

    private void EnsureAssignedTantou(Guid tantouId)
    {
        if (AssignedEditorId != tantouId)
            throw new UnauthorizedAccessException("Only the assigned Tantou Editor can perform this action.");
    }

    public void Approve()
    {
        if (Status != ChapterStatus.PendingEditorialReview && Status != ChapterStatus.ConflictEscalated)
            throw new InvalidOperationException("Only the Editorial Board or Editor in Chief can approve this chapter.");
        Status = ChapterStatus.Approved;
    }
    public void RequestQaRevision() => Status = ChapterStatus.QaRevisionRequired;
    public void Archive() => Status = ChapterStatus.Archived;

    public void Reopen()
    {
        if (Status != ChapterStatus.Approved)
            throw new InvalidOperationException("Only Approved chapters can be reopened for QA.");
        Status = ChapterStatus.ReadyForQA;
    }

    public void Delete()
    {
        if (Status != ChapterStatus.Draft)
            throw new InvalidOperationException("Only Draft chapters can be deleted.");

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

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

    public void CancelPublishSchedule()
    {
        ScheduledPublishAt = null;
    }
}

public class PageTask : AggregateRoot, ISoftDeletable
{
    public Guid ChapterId { get; set; }
    public int PageNumber { get; set; }
    public Guid? AssignedAssistantId { get; set; }
    public string? Description { get; set; }
    /// <summary>Original page image uploaded by the Mangaka for this task.</summary>
    public string BaseImageUrl { get; private set; } = string.Empty;
    public PageTaskStatus TaskStatus { get; set; } = PageTaskStatus.Pending;
    /// <summary>SAM-generated mask polygon stored as JSON (array of [x,y] points).</summary>
    public string? RegionMask { get; set; }
    /// <summary>Type of artwork work for this region (Background, Shading, etc.).</summary>
    public PageTaskType TaskType { get; set; } = PageTaskType.General;
    public DateTime? Deadline { get; set; }
    public DateTime? WorkStartedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public Guid? PrimaryAssistantId { get; set; }
    public Guid? BackupAssistantId { get; set; }
    public Guid? CurrentAssignmentAttemptId { get; set; }
    public string? TakeoverStatus { get; set; } = "None"; // "None" | "TakeoverRequested" | "TakeoverAccepted" | "TakeoverFailed"
    public string? ReassignmentReason { get; set; }
    public DateTime? ReassignmentRequiredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public virtual Chapter Chapter { get; set; } = null!;

    public void SetDeadline(DateTime? deadline)
    {
        Deadline = deadline;
        UpdatedAt = DateTime.UtcNow;
    }
    public virtual PreviewPage? PreviewPage { get; set; }
    public virtual ICollection<BasePageVersion> BasePageVersions { get; private set; } = new List<BasePageVersion>();

    public void UpdateBaseImage(string newImageUrl, Guid updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(newImageUrl))
            throw new ArgumentException("BaseImageUrl cannot be empty.");

        BaseImageUrl = newImageUrl.Trim();
        UpdatedAt = DateTime.UtcNow;

        var nextVersion = BasePageVersions.Any() ? BasePageVersions.Max(v => v.VersionNumber) + 1 : 1;
        BasePageVersions.Add(BasePageVersion.Create(Id, nextVersion, BaseImageUrl, updatedByUserId));
    }

    public void AssignPrimaryAndBackup(Guid primaryAssistantId, Guid? backupAssistantId, string? description = null, DateTime? deadline = null)
    {
        if (TaskStatus != PageTaskStatus.Pending && TaskStatus != PageTaskStatus.ReassignmentRequired && TaskStatus != PageTaskStatus.Incomplete && TaskStatus != PageTaskStatus.PendingAcceptance)
            throw new InvalidOperationException($"Cannot assign task in status '{TaskStatus}'.");

        PrimaryAssistantId = primaryAssistantId;
        BackupAssistantId = backupAssistantId;
        AssignedAssistantId = primaryAssistantId;
        Description = description ?? Description;
        Deadline = deadline ?? Deadline;
        TaskStatus = PageTaskStatus.PendingAcceptance;
        TakeoverStatus = "None";
        ReassignmentReason = null;
        ReassignmentRequiredAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RequestTakeover(string reason)
    {
        if (BackupAssistantId is null)
            throw new InvalidOperationException("No backup assistant assigned for this task.");

        TakeoverStatus = "TakeoverRequested";
        ReassignmentReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AcceptTakeover(Guid backupAssistantId, DateTime acceptedAt, DateTime newDeadline)
    {
        if (BackupAssistantId != backupAssistantId)
            throw new UnauthorizedAccessException("Only the assigned backup assistant can take over this task.");

        AssignedAssistantId = backupAssistantId;
        TaskStatus = PageTaskStatus.Incomplete;
        TakeoverStatus = "TakeoverAccepted";
        WorkStartedAt = acceptedAt;
        Deadline = newDeadline;
        UpdatedAt = acceptedAt;
    }

    public void AssignPending(Guid assistantId, string? description = null, DateTime? deadline = null)
    {
        AssignPrimaryAndBackup(assistantId, null, description, deadline);
    }

    public void AcceptAssignment(DateTime acceptedAt, TimeSpan? duration = null)
    {
        if (TaskStatus != PageTaskStatus.PendingAcceptance)
            throw new InvalidOperationException("Only PendingAcceptance tasks can be accepted.");

        TaskStatus = PageTaskStatus.Incomplete;
        WorkStartedAt = acceptedAt;
        if (duration.HasValue && duration.Value > TimeSpan.Zero)
        {
            Deadline = acceptedAt.Add(duration.Value);
        }
        UpdatedAt = acceptedAt;
    }

    public void RejectAssignment()
    {
        AssignedAssistantId = null;
        WorkStartedAt = null;
        TaskStatus = PageTaskStatus.ReassignmentRequired;
        ReassignmentReason = "Primary assistant rejected assignment.";
        ReassignmentRequiredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public int ProgressPercent { get; set; } = 0;

    public void MarkReassignmentRequired(string? reason = null)
    {
        AssignedAssistantId = null;
        WorkStartedAt = null;
        Deadline = null;
        TaskStatus = PageTaskStatus.ReassignmentRequired;
        ReassignmentReason = reason ?? "All candidate assistants rejected or timed out.";
        ReassignmentRequiredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SubmitProgress(int percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentException("Progress percent must be between 0 and 100.");
        if (percent < ProgressPercent)
            throw new InvalidOperationException("Progress percent cannot decrease.");

        ProgressPercent = percent;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteTask(DateTime completedAt)
    {
        if (TaskStatus != PageTaskStatus.Incomplete && TaskStatus != PageTaskStatus.RevisionAlert)
            throw new InvalidOperationException($"Cannot complete task in status '{TaskStatus}'.");

        ProgressPercent = 100;
        TaskStatus = PageTaskStatus.Reviewing;
        UpdatedAt = completedAt;
    }

    public static PageTask CreatePending(Guid chapterId, int pageNumber, string baseImageUrl)
    {
        if (pageNumber <= 0)
            throw new ArgumentException("PageNumber must be > 0.");
        if (string.IsNullOrWhiteSpace(baseImageUrl))
            throw new ArgumentException("BaseImageUrl is required.");

        return new PageTask
        {
            ChapterId = chapterId,
            PageNumber = pageNumber,
            BaseImageUrl = baseImageUrl.Trim(),
            TaskStatus = PageTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Activate(Guid assistantId, PageTaskType taskType, string? description = null, DateTime? deadline = null)
    {
        if (TaskStatus != PageTaskStatus.Pending)
            throw new InvalidOperationException("Only Pending page tasks can be activated.");

        AssignedAssistantId = assistantId;
        TaskType = taskType;
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

    public void ReopenForFix(Guid assistantId, string? description = null)
    {
        AssignedAssistantId = assistantId;
        if (description != null) Description = description;
        TaskStatus = PageTaskStatus.Incomplete;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RequestRevision()
    {
        if (TaskStatus != PageTaskStatus.Reviewing)
            throw new InvalidOperationException("Only Reviewing page tasks can be sent for revision.");

        TaskStatus = PageTaskStatus.RevisionAlert;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        AssignedAssistantId = null;
        TaskStatus = PageTaskStatus.Pending;
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

public class BasePageVersion
{
    public Guid Id { get; private set; }
    public Guid PageTaskId { get; private set; }
    public int VersionNumber { get; private set; }
    public string BaseImageUrl { get; private set; } = string.Empty;
    public Guid UpdatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BasePageVersion() { }

    public static BasePageVersion Create(Guid pageTaskId, int versionNumber, string baseImageUrl, Guid updatedByUserId)
    {
        return new BasePageVersion
        {
            Id = Guid.NewGuid(),
            PageTaskId = pageTaskId,
            VersionNumber = versionNumber,
            BaseImageUrl = baseImageUrl,
            UpdatedByUserId = updatedByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
