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
    /// Lấy queue cho Editorial Board: submissions đang Pending_EB_Review.
    /// </summary>
    Task<IEnumerable<SeriesSubmission>> GetRecommendedQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra Mangaka đã có submission đang active chưa (Draft, Pending_EB_Review, Requires_Revision).
    /// Dùng để giới hạn 1 submission active per Mangaka per series title.
    /// </summary>
    Task<bool> HasActiveSubmissionAsync(Guid submitterId, string title, CancellationToken ct = default);

    // ── Writes ─────────────────────────────────────────────────────────────────

    /// <summary>Thêm submission mới vào DbContext (chưa save).</summary>
    Task AddAsync(SeriesSubmission submission, CancellationToken ct = default);

    /// <summary>Persist tất cả thay đổi đang tracked.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    // ── Feedback Pins ─────────────────────────────────────────────────────────

    /// <summary>Lấy danh sách feedback pins đang active (chưa archived) của một submission.</summary>
    Task<IEnumerable<SubmissionFeedbackPin>> GetActivePinsBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default);

    /// <summary>Lấy tất cả feedback pins (bao gồm archived) của một submission — dùng cho history view.</summary>
    Task<IEnumerable<SubmissionFeedbackPin>> GetAllPinsBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default);

    /// <summary>Thêm feedback pin mới vào DbContext (chưa save).</summary>
    Task AddPinAsync(SubmissionFeedbackPin pin, CancellationToken ct = default);
}
