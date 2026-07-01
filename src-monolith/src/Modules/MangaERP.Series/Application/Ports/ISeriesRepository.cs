using MangaERP.Series.Domain.Entities;

namespace MangaERP.Series.Application.Ports;

/// <summary>
/// Port (interface) định nghĩa data access contract cho Series module.
/// Được implement bởi SeriesRepository trong Infrastructure layer.
/// </summary>
public interface ISeriesRepository
{
    /// <summary>Lấy series theo Id.</summary>
    Task<MangaSeries?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lấy tất cả series của một Mangaka (authorId).</summary>
    Task<IEnumerable<MangaSeries>> GetByAuthorIdAsync(Guid authorId, CancellationToken ct = default);

    /// <summary>Lấy tất cả series (Admin / EB view).</summary>
    Task<IEnumerable<MangaSeries>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách series đang có yêu cầu hủy chờ duyệt (CancellationStatus == Pending).
    /// Dùng cho Board EB/EIC cancellation-queue.
    /// </summary>
    Task<IEnumerable<MangaSeries>> GetCancellationQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy series theo submissionId — dùng để kiểm tra series đã được tạo chưa
    /// khi approve một submission.
    /// </summary>
    Task<MangaSeries?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default);

    /// <summary>Thêm series mới vào DbContext (chưa save).</summary>
    Task AddAsync(MangaSeries series, CancellationToken ct = default);

    /// <summary>Persist tất cả thay đổi đang tracked.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

