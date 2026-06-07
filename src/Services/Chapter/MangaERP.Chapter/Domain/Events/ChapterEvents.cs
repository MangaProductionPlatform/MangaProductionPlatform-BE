using MangaERP.BuildingBlocks.Domain.Abstractions;

namespace MangaERP.Chapter.Domain.Entities;

public record ChapterSubmittedForQA(Guid EventId, DateTime OccurredOn, Guid ChapterId, Guid SeriesId) : IDomainEvent;

public record ChapterApproved(Guid EventId, DateTime OccurredOn, Guid ChapterId, Guid SeriesId) : IDomainEvent;

public record AllPagesCompleted(Guid EventId, DateTime OccurredOn, Guid ChapterId) : IDomainEvent;
