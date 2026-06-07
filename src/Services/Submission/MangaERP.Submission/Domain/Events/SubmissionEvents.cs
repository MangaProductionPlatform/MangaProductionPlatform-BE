using MangaERP.BuildingBlocks.Domain.Abstractions;

namespace MangaERP.Submission.Domain.Events;

public record SubmissionApproved(Guid EventId, DateTime OccurredOn, Guid SubmissionId, Guid SubmitterId) : IDomainEvent;

public record SubmissionRejected(Guid EventId, DateTime OccurredOn, Guid SubmissionId, Guid SubmitterId) : IDomainEvent;
