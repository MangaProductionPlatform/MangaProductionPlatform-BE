using MangaERP.Publishing.Domain.Entities;

namespace MangaERP.Publishing.Application.Ports;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface INotificationService
{
    Task NotifyTaskAssignedAsync(Guid assistantId, Guid pageTaskId, int pageNumber, CancellationToken ct = default);
    Task NotifyRevisionRequiredAsync(Guid assistantId, Guid pageTaskId, string rejectionNote, CancellationToken ct = default);
    Task NotifyTaskApprovedAsync(Guid assistantId, Guid pageTaskId, CancellationToken ct = default);
    Task NotifyChapterReadyForQAAsync(Guid chapterId, string chapterTitle, CancellationToken ct = default);
}
