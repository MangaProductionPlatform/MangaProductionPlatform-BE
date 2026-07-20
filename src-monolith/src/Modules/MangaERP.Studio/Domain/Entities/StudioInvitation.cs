using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Studio.Domain.Entities;

/// <summary>
/// Trạng thái của lời mời vào studio.
/// </summary>
public enum StudioInvitationStatus
{
    /// Đang chờ người được mời xử lý (TH2: đã có tài khoản)
    Pending,
    /// Người được mời đã chấp nhận
    Accepted,
    /// Người được mời đã từ chối
    Declined,
    /// Link kích hoạt đã hết hạn (TH1: chưa có tài khoản)
    Expired,
    /// Đã hủy bởi Mangaka
    Cancelled
}

public enum RegistrationDeliveryStatus { NotRequired, Pending, Sent, Failed }

/// <summary>
/// Thực thể lời mời Assistant vào studio của Mangaka.
/// Hỗ trợ 2 trường hợp:
/// - TH1: Email chưa có tài khoản → tạo tài khoản PendingActivation + gửi email kích hoạt.
///         Sau khi kích hoạt và đăng nhập, Assistant vẫn phải chấp nhận lời mời đang chờ.
/// - TH2: Email đã có tài khoản Assistant → gửi push notification, Assistant tự accept/decline.
/// </summary>
public class StudioInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// ID của Mangaka gửi lời mời
    public Guid InviterMangakaId { get; set; }

    /// ID của Series (Studio scope)
    public Guid SeriesId { get; set; }

    /// Email của Assistant được mời
    public string AssistantEmail { get; set; } = string.Empty;
    public string NormalizedAssistantEmail { get; set; } = string.Empty;

    /// UserId của Assistant (null nếu TH1 — chưa tồn tại tài khoản khi mời)
    public Guid? AssistantUserId { get; set; }

    /// Lời nhắn từ Mangaka
    public string? Message { get; set; }

    /// Phân biệt TH1 (email mới, tạo tài khoản) vs TH2 (đã có tài khoản)
    public bool IsNewAccountFlow { get; set; } = false;

    /// InvitationToken dùng cho TH1 (link trong email kích hoạt)
    /// Khi activate xong, backend tra cứu token này để auto-accept
    public string? ActivationToken { get; set; }

    public StudioInvitationStatus Status { get; set; } = StudioInvitationStatus.Pending;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public RegistrationDeliveryStatus RegistrationDeliveryStatus { get; private set; } = RegistrationDeliveryStatus.NotRequired;
    public DateTime? RegistrationDeliveryAttemptedAt { get; private set; }
    public string? RegistrationDeliveryError { get; private set; }

    public void MarkRegistrationDeliveryPending() => RegistrationDeliveryStatus = RegistrationDeliveryStatus.Pending;

    public void MarkRegistrationDeliverySent()
    {
        RegistrationDeliveryStatus = RegistrationDeliveryStatus.Sent;
        RegistrationDeliveryAttemptedAt = DateTime.UtcNow;
        RegistrationDeliveryError = null;
    }

    public void MarkRegistrationDeliveryFailed(string error)
    {
        RegistrationDeliveryStatus = RegistrationDeliveryStatus.Failed;
        RegistrationDeliveryAttemptedAt = DateTime.UtcNow;
        RegistrationDeliveryError = string.IsNullOrWhiteSpace(error) ? "Registration delivery failed." : error[..Math.Min(error.Length, 1000)];
    }
}
