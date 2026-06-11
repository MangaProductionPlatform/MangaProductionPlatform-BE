using MangaERP.Submission.Domain.Entities;

namespace MangaERP.Submission.Application.Ports;

/// <summary>
/// Port (interface) định nghĩa data access contract cho Submission module.
/// Được implement bởi SubmissionRepository trong Infrastructure layer.
/// </summary>
public interface ISubmissionRepository
{
    // ── Reads ──────────────────────────────────────────────────────────────────

    /// <summary>Lấy submission theo Id. Trả null nếu không tìm thấy.</summary>
    Task<SeriesSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lấy tất cả submissions của một Mangaka (theo submitterId).</summary>
    Task<IEnumerable<SeriesSubmission>> GetBySubmitterIdAsync(Guid submitterId, CancellationToken ct = default);

    /// <summary>
    /// Lấy queue cho Tantou Editor: submissions đang Pending hoặc UnderReview.
    /// </summary>
    Task<IEnumerable<SeriesSubmission>> GetPendingQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy queue cho Editorial Board: submissions đang RecommendedToBoard.
    /// </summary>
    Task<IEnumerable<SeriesSubmission>> GetRecommendedQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra Mangaka đã có submission đang active chưa (Draft, Pending, UnderReview, RecommendedToBoard).
    /// Dùng để giới hạn 1 submission active per Mangaka per series title.
    /// </summary>
    Task<bool> HasActiveSubmissionAsync(Guid submitterId, string title, CancellationToken ct = default);

    // ── Writes ─────────────────────────────────────────────────────────────────

    /// <summary>Thêm submission mới vào DbContext (chưa save).</summary>
    Task AddAsync(SeriesSubmission submission, CancellationToken ct = default);

    /// <summary>Persist tất cả thay đổi đang tracked.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
