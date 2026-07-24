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

    /// <summary>Lấy TẤT CẢ submissions — dùng cho Admin Dashboard stats (in-memory aggregation).</summary>
    Task<IEnumerable<SeriesSubmission>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy submission theo Id với PESSIMISTIC ROW-LEVEL LOCK (SELECT FOR UPDATE).
    /// Chỉ dùng trong transaction — đảm bảo tại một thời điểm chỉ có 1 luồng
    /// đọc + ghi cho SubmissionId này, tránh race condition khi tính phiếu bầu.
    /// </summary>
    Task<SeriesSubmission?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lấy tất cả submissions của một Mangaka (theo submitterId).</summary>
    Task<IEnumerable<SeriesSubmission>> GetBySubmitterIdAsync(Guid submitterId, CancellationToken ct = default);

    /// <summary>
    /// Lấy queue cho Editorial Board: submissions đang Pending_EB_Review.
    /// </summary>
    Task<IEnumerable<SeriesSubmission>> GetRecommendedQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy queue cho Editor-in-Chief: Conflict_Escalated đứng trước, sau đó Pending_EB_Review.
    /// </summary>
    Task<IEnumerable<SeriesSubmission>> GetEICQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách submissions Pending_EB_Review mà user chưa vote trong vòng hiện tại.
    /// Thực hiện hoàn toàn server-side (single query) — RoundNumber được so khớp với s.CurrentRound.
    /// </summary>
    Task<IEnumerable<SeriesSubmission>> GetPendingQueueNotVotedByAsync(Guid editorId, CancellationToken ct = default);

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

    // ── Submission Votes ──────────────────────────────────────────────────────

    /// <summary>Kiểm tra editor đã vote cho submission trong vòng cụ thể chưa.</summary>
    Task<bool> HasVotedAsync(Guid submissionId, Guid editorId, int roundNumber, CancellationToken ct = default);

    /// <summary>Lấy tất cả phiếu bầu của một submission trong vòng cụ thể.</summary>
    Task<IEnumerable<SubmissionVote>> GetVotesByRoundAsync(Guid submissionId, int roundNumber, CancellationToken ct = default);

    /// <summary>Thêm vote mới vào DbContext (chưa save).</summary>
    Task AddVoteAsync(SubmissionVote vote, CancellationToken ct = default);

    /// <summary>
    /// [Admin force-action cleanup] Xóa tất cả phiếu bầu của một submission trong một vòng cụ thể.
    /// Dùng khi Admin buộc phải override kết quả đang trong luồng bỏ phiếu dở dang.
    /// Phải gọi trong cùng transaction với thay đổi trạng thái submission.
    /// </summary>
    Task DeleteVotesByRoundAsync(Guid submissionId, int roundNumber, CancellationToken ct = default);

    /// <summary>
    /// [Admin force-action cleanup] Xóa tất cả phiếu bầu của một submission (mọi vòng).
    /// Dùng khi Admin buộc phải reject/approve hoàn toàn, đóng băng submission vĩnh viễn.
    /// </summary>
    Task DeleteAllVotesAsync(Guid submissionId, CancellationToken ct = default);

    /// <summary>
    /// Tự động gán 2 Reviewers thuộc Editorial Board cho vòng hiện tại của submission và lưu persistent notification trong cùng transaction.
    /// </summary>
    Task AssignEditorialReviewersAsync(Guid submissionId, int roundNumber, string authorName, CancellationToken ct = default);
}
