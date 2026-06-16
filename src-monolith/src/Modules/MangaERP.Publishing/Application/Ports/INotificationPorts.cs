using MangaERP.Publishing.Domain.Entities;

namespace MangaERP.Publishing.Application.Ports;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

