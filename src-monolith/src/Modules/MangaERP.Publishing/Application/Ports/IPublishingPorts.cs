using MangaERP.Publishing.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MangaERP.Publishing.Application.Ports;

public interface IPublicationRecordRepository
{
    System.Threading.Tasks.Task<PublicationRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<PublicationRecord?> GetByChapterIdAsync(Guid chapterId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<PublicationRecord>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(PublicationRecord record, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(PublicationRecord record, CancellationToken ct = default);
}

public interface INotificationRepository
{
    System.Threading.Tasks.Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<Notification>> GetUnreadByReceiverAsync(Guid receiverId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<Notification>> GetAllByReceiverAsync(Guid receiverId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(Notification notification, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(Notification notification, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Đánh dấu tất cả thông báo chưa đọc của một receiver là đã đọc trong một lần.
    /// Trả về số lượng thông báo đã được cập nhật.
    /// </summary>
    System.Threading.Tasks.Task<int> MarkAllAsReadAsync(Guid receiverId, CancellationToken ct = default);

    /// <summary>Đếm số thông báo chưa đọc của receiver (dùng cho badge Navbar).</summary>
    System.Threading.Tasks.Task<int> CountUnreadAsync(Guid receiverId, CancellationToken ct = default);

    /// <summary>Xóa một thông báo cụ thể (chỉ xóa nếu thuộc về receiver).</summary>
    System.Threading.Tasks.Task DeleteAsync(Guid notificationId, Guid receiverId, CancellationToken ct = default);

    /// <summary>Xóa tất cả thông báo đã đọc của receiver (bulk cleanup).</summary>
    System.Threading.Tasks.Task<int> DeleteAllReadAsync(Guid receiverId, CancellationToken ct = default);
}
