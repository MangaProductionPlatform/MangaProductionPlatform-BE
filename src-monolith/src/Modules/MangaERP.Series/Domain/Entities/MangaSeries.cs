using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Series.Domain.Entities;

public enum SeriesStatus { Active, Hiatus, Cancelled }

/// <summary>
/// Trạng thái của yêu cầu hủy series — độc lập với SeriesStatus.
/// </summary>
public enum CancellationRequestStatus { None, Pending, Approved, Rejected }

public class MangaSeries : AggregateRoot, ISoftDeletable
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string? Genre { get; private set; }
    public SeriesStatus Status { get; private set; } = SeriesStatus.Active;
    public Guid AuthorId { get; private set; }
    public Guid? SubmissionId { get; private set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    // ── Cancellation Request Fields ───────────────────────────────────────────
    public CancellationRequestStatus CancellationStatus { get; private set; } = CancellationRequestStatus.None;
    public string? CancellationReason { get; private set; }
    public Guid? CancellationRequestedById { get; private set; }
    public DateTime? CancellationRequestedAt { get; private set; }
    public Guid? CancellationReviewedById { get; private set; }
    public DateTime? CancellationReviewedAt { get; private set; }
    public string? CancellationRejectReason { get; private set; }

    private MangaSeries() { }

    public static MangaSeries Create(Guid authorId, Guid? submissionId, string title,
        string? description, string? genre, string? coverImageUrl)
        => new() { AuthorId = authorId, SubmissionId = submissionId, Title = title,
                   Description = description, Genre = genre, CoverImageUrl = coverImageUrl,
                   Status = SeriesStatus.Active, CreatedAt = DateTime.UtcNow };

    // ── Trực tiếp cancel (admin/legacy) ──────────────────────────────────────
    public void Cancel()
    {
        if (Status == SeriesStatus.Cancelled)
            throw new InvalidOperationException("Series is already cancelled.");
        Status = SeriesStatus.Cancelled;
    }

    public void SetHiatus() => Status = SeriesStatus.Hiatus;
    public void Reactivate() => Status = SeriesStatus.Active;

    // ── Cancellation Request Flow (MF1) ───────────────────────────────────────

    /// <summary>Mangaka gửi yêu cầu hủy series.</summary>
    public void RequestCancellation(Guid requesterId, string reason)
    {
        if (Status == SeriesStatus.Cancelled)
            throw new InvalidOperationException("Series đã bị hủy rồi.");
        if (AuthorId != requesterId)
            throw new UnauthorizedAccessException("Chỉ tác giả mới được gửi yêu cầu hủy series.");
        if (CancellationStatus == CancellationRequestStatus.Pending)
            throw new InvalidOperationException("Đã có yêu cầu hủy đang chờ duyệt.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Lý do hủy không được để trống.");

        CancellationStatus = CancellationRequestStatus.Pending;
        CancellationReason = reason;
        CancellationRequestedById = requesterId;
        CancellationRequestedAt = DateTime.UtcNow;
        CancellationReviewedById = null;
        CancellationReviewedAt = null;
        CancellationRejectReason = null;
    }

    /// <summary>EB/EIC chấp thuận yêu cầu hủy → series chuyển sang Cancelled.</summary>
    public void ApproveCancellation(Guid reviewerId)
    {
        if (CancellationStatus != CancellationRequestStatus.Pending)
            throw new InvalidOperationException("Không có yêu cầu hủy nào đang chờ duyệt.");

        CancellationStatus = CancellationRequestStatus.Approved;
        CancellationReviewedById = reviewerId;
        CancellationReviewedAt = DateTime.UtcNow;
        Status = SeriesStatus.Cancelled;
    }

    /// <summary>EB/EIC từ chối yêu cầu hủy → series vẫn Active/Hiatus.</summary>
    public void RejectCancellation(Guid reviewerId, string rejectReason)
    {
        if (CancellationStatus != CancellationRequestStatus.Pending)
            throw new InvalidOperationException("Không có yêu cầu hủy nào đang chờ duyệt.");
        if (string.IsNullOrWhiteSpace(rejectReason))
            throw new ArgumentException("Lý do từ chối không được để trống.");

        CancellationStatus = CancellationRequestStatus.Rejected;
        CancellationReviewedById = reviewerId;
        CancellationReviewedAt = DateTime.UtcNow;
        CancellationRejectReason = rejectReason;
    }
}

