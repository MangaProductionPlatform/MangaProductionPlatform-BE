using MediatR;

namespace MangaERP.Shared.Application.Contracts.Events;

public record SegmentationTaskAssignedEvent(
    Guid TaskId,
    Guid AssignedToUserId,
    Guid CreatedByUserId,
    Guid PageId,
    string TaskType
) : INotification;
