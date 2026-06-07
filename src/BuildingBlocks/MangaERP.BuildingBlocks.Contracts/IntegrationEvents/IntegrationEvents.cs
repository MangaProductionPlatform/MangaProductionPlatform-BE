namespace MangaERP.BuildingBlocks.Contracts.IntegrationEvents;

/// <summary>Published by Submission service when a series proposal is approved. Consumed by Series service.</summary>
public record SubmissionApprovedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid SubmissionId,
    Guid SeriesId,
    Guid MangakaUserId,
    string SeriesTitle,
    string? Genre,
    string? CoverImageUrl);

/// <summary>Published by QA service when a chapter passes all checks. Consumed by Publishing service.</summary>
public record ChapterApprovedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid ChapterId,
    Guid SeriesId,
    Guid EditorId);

/// <summary>Published by Task service when a layer is accepted by the Mangaka. Consumed by Chapter service to progress page state.</summary>
public record LayerAcceptedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid PageTaskId,
    Guid ArtworkLayerId);

/// <summary>Published by Task service when a layer is rejected. Consumed by Notification service to alert the assistant.</summary>
public record LayerRejectedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid PageTaskId,
    Guid ArtworkLayerId,
    Guid AssistantId,
    string RejectionNote);

/// <summary>Published by Chapter service when a chapter is submitted for editorial QA. Consumed by QA service.</summary>
public record ChapterSubmittedForQAEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid ChapterId,
    Guid SeriesId,
    Guid? AssignedEditorId);

/// <summary>Published by Publishing service after auto-publish. Consumed by Notification and Ranking services.</summary>
public record ChapterPublishedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid ChapterId,
    Guid SeriesId,
    string PublicationUrl,
    string CacheKey,
    DateTime PublishedAt);

/// <summary>Published by Task service when a task is assigned. Consumed by Notification service.</summary>
public record TaskAssignedEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid PageTaskId,
    Guid ChapterId,
    Guid AssistantId,
    int PageNumber);
