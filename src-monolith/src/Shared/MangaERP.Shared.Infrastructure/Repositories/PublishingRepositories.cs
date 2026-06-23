using MangaERP.Publishing.Application.Ports;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class PublishingRepositories : IPublicationRecordRepository, INotificationRepository
{
    private readonly AppDbContext _db;

    public PublishingRepositories(IDbContextProvider provider)
    {
        _db = (AppDbContext)provider.GetDbContext();
    }

    // ── IPublicationRecordRepository Implementation ────────────────

    public async System.Threading.Tasks.Task<PublicationRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.PublicationRecords.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async System.Threading.Tasks.Task<PublicationRecord?> GetByChapterIdAsync(Guid chapterId, CancellationToken ct = default)
        => await _db.PublicationRecords.FirstOrDefaultAsync(p => p.ChapterId == chapterId, ct);

    public async System.Threading.Tasks.Task<IEnumerable<PublicationRecord>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default)
        => await _db.PublicationRecords
            .Where(p => p.SeriesId == seriesId)
            .OrderByDescending(p => p.PublishedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddAsync(PublicationRecord record, CancellationToken ct = default)
    {
        await _db.PublicationRecords.AddAsync(record, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task UpdateAsync(PublicationRecord record, CancellationToken ct = default)
    {
        _db.PublicationRecords.Update(record);
        await _db.SaveChangesAsync(ct);
    }

    // ── INotificationRepository Implementation ─────────────────────

    async System.Threading.Tasks.Task<Notification?> INotificationRepository.GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    async System.Threading.Tasks.Task<IEnumerable<Notification>> INotificationRepository.GetUnreadByReceiverAsync(Guid receiverId, CancellationToken ct)
        => await _db.Notifications
            .Where(n => n.ReceiverId == receiverId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    async System.Threading.Tasks.Task<IEnumerable<Notification>> INotificationRepository.GetAllByReceiverAsync(Guid receiverId, CancellationToken ct)
        => await _db.Notifications
            .Where(n => n.ReceiverId == receiverId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    async System.Threading.Tasks.Task INotificationRepository.AddAsync(Notification notification, CancellationToken ct)
        // Only stages the entity — caller is responsible for calling SaveChangesAsync.
        => await _db.Notifications.AddAsync(notification, ct);

    async System.Threading.Tasks.Task INotificationRepository.UpdateAsync(Notification notification, CancellationToken ct)
    {
        _db.Notifications.Update(notification);
        await _db.SaveChangesAsync(ct);
    }

    async System.Threading.Tasks.Task<int> INotificationRepository.SaveChangesAsync(CancellationToken ct)
        => await _db.SaveChangesAsync(ct);
}
