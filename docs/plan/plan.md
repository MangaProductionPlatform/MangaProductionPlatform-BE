# Kế Hoạch Triển Khai: Visual Feedback Pinpointing & Submission Refactor

> **Nguyên tắc:** Mỗi Phase là 1 đơn vị triển khai độc lập, có thể commit riêng. Ưu tiên sửa cái đang chạy trước, thêm tính năng mới sau.

---

## Phase 1: Dọn Dẹp Code Cũ (Xóa Luồng TE 2 Tầng)

### Bối cảnh hiện tại
Codebase vẫn còn tàn dư luồng 2 tầng: `StartReview`, `RecommendToBoard` commands + controller routes + import statements, mặc dù Domain Entity (`SeriesSubmission.cs`) đã refactor sang 1 tầng (không còn `Pending_TE_Review`).

### Bước 1.1: Xóa Command/Handler cũ
*   Xóa toàn bộ thư mục:
    *   `Application/Commands/StartReview/`
    *   `Application/Commands/RecommendToBoard/`
    *   *(Nếu thư mục không tồn tại thì bỏ qua — handler có thể đã bị xóa nhưng import chưa dọn)*

### Bước 1.2: Dọn Controller `SubmissionsController.cs`
*   Xóa `using` cho `StartReview` và `RecommendToBoard`.
*   Xóa endpoint `POST {id}/start-review` (line 205-220).
*   Xóa endpoint `POST {id}/recommend` (line 226-241).
*   Xóa endpoint `POST {id}/te-request-revision` (line 247-262) — TE không còn quyền request revision trong luồng 1 tầng.
*   Xóa endpoint `POST {id}/te-reject` (line 289-304) — TE không còn quyền reject submission.
*   Cập nhật XML comment các endpoint còn lại: `submit` → `Pending_EB_Review` (không phải `Pending_TE_Review`), `resubmit` → `Pending_EB_Review`.
*   Xóa `RecommendRequest` record.
*   Cập nhật `GetQueue` comment: chỉ EB/Admin xem được.

### Bước 1.3: Cập nhật `GetSubmissionQueueHandler.cs`
*   Xóa comment cũ referencing TE queue.
*   Giữ logic hiện tại (đã chỉ cho EB/Admin xem `Pending_EB_Review`).

### Bước 1.4: Cập nhật `RejectSubmissionHandler.cs`
*   Command vẫn giữ `ActorRole` field nhưng Domain Entity đã guard chỉ cho `EditorialBoard`. Handler OK.
*   Xóa controller endpoint `te-reject` ở bước 1.2 là đủ.

### Bước 1.5: Cập nhật `submission_state_machine.md`
*   Xóa các hàng `start-review`, `recommend`, `te-request-revision`, `te-reject` trong bảng mapping.
*   Xóa trạng thái `Pending_TE_Review` và `TE_Rejected` khỏi danh sách.
*   Cập nhật code mẫu C# cho khớp với `SeriesSubmission.cs` hiện tại.

---

## Phase 2: Bổ Sung `AssignedEditorId` Vào Luồng Approve

### Bối cảnh hiện tại
`ApproveSubmissionHandler` chưa nhận `AssignedEditorId`. Khi EB approve, cần gán TE phụ trách Series mới tạo.

### Bước 2.1: Cập nhật `ApproveSubmissionCommand`
```csharp
public record ApproveSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewerId,
    Guid AssignedEditorId  // ← THÊM MỚI
) : IRequest<ApproveSubmissionResult>;
```

### Bước 2.2: Cập nhật `ApproveSubmissionHandler`
1.  Validate `AssignedEditorId` tồn tại và có role `TantouEditor` (dùng `IUserRepository`).
2.  Trong atomic transaction:
    *   `submission.Approve(cmd.ReviewerId)` — Domain guard.
    *   Tạo `MangaSeries` (giữ nguyên).
    *   Lấy User (Mangaka) → set `ManagingTantouId = cmd.AssignedEditorId`.
    *   SaveChanges + Commit.

### Bước 2.3: Cập nhật `ApproveSubmissionValidator`
```csharp
RuleFor(x => x.AssignedEditorId).NotEmpty()
    .WithMessage("Phải chỉ định Tantou Editor phụ trách.");
```

### Bước 2.4: Cập nhật Controller endpoint `approve`
*   Tạo `ApproveRequest` record nhận `AssignedEditorId` từ body.
*   Truyền vào command.

---

## Phase 3: Tạo Entity `SubmissionFeedbackPin`

### Bước 3.1: Tạo Entity trong Domain Layer
File: `Submission/Domain/Entities/SubmissionFeedbackPin.cs`

```csharp
public enum FeedbackPinCategory { Visual, Content, Typo }

public class SubmissionFeedbackPin
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SubmissionId { get; private set; }
    public string PageIdentifier { get; private set; } = string.Empty;
    public double CoordinateX { get; private set; } // 0-100%
    public double CoordinateY { get; private set; } // 0-100%
    public string Comment { get; private set; } = string.Empty;
    public FeedbackPinCategory Category { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public bool IsArchived { get; private set; } = false;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private SubmissionFeedbackPin() { }

    public static SubmissionFeedbackPin Create(
        Guid submissionId, string pageIdentifier, double x, double y,
        string comment, FeedbackPinCategory category, Guid createdByUserId)
    {
        if (x < 0 || x > 100 || y < 0 || y > 100)
            throw new ArgumentException("Coordinates must be between 0 and 100.");
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Comment cannot be empty.");

        return new SubmissionFeedbackPin
        {
            SubmissionId = submissionId, PageIdentifier = pageIdentifier,
            CoordinateX = x, CoordinateY = y, Comment = comment,
            Category = category, CreatedByUserId = createdByUserId
        };
    }

    public void Archive() => IsArchived = true;
}
```

### Bước 3.2: Cấu hình EF Core (`ModuleConfigurations.cs`)
*   Thêm relationship trong `SeriesSubmissionConfiguration`:
    ```csharp
    b.HasMany<SubmissionFeedbackPin>()
     .WithOne().HasForeignKey(p => p.SubmissionId)
     .OnDelete(DeleteBehavior.Cascade);
    ```
*   Thêm class `SubmissionFeedbackPinConfiguration`:
    ```csharp
    public class SubmissionFeedbackPinConfiguration : IEntityTypeConfiguration<SubmissionFeedbackPin>
    {
        public void Configure(EntityTypeBuilder<SubmissionFeedbackPin> b)
        {
            b.ToTable("SubmissionFeedbackPins"); b.HasKey(e => e.Id);
            b.Property(e => e.PageIdentifier).IsRequired().HasMaxLength(2048);
            b.Property(e => e.CoordinateX).HasColumnType("decimal(5,2)");
            b.Property(e => e.CoordinateY).HasColumnType("decimal(5,2)");
            b.Property(e => e.Comment).IsRequired().HasMaxLength(2000);
            b.Property(e => e.Category).HasConversion(
                v => v.ToString(), v => Enum.Parse<FeedbackPinCategory>(v)).HasMaxLength(50);
            b.HasIndex(e => new { e.SubmissionId, e.IsArchived });
        }
    }
    ```

### Bước 3.3: Đăng ký DbSet trong `AppDbContext`
```csharp
public DbSet<SubmissionFeedbackPin> SubmissionFeedbackPins => Set<SubmissionFeedbackPin>();
```

### Bước 3.4: Thêm Repository method
*   `ISubmissionRepository`: thêm `Task<IEnumerable<SubmissionFeedbackPin>> GetActivePinsBySubmissionIdAsync(Guid submissionId, CancellationToken ct)`.
*   `SubmissionRepository`: implement query `WHERE SubmissionId = @id AND IsArchived = false`.

### Bước 3.5: Tạo EF Migration
```bash
dotnet ef migrations add AddSubmissionFeedbackPins
```

---

## Phase 4: Refactor `RequestRevisionHandler` Tích Hợp Feedback Pins

### Bước 4.1: Cập nhật Command
```csharp
public record FeedbackPinInput(
    string PageIdentifier, double CoordinateX, double CoordinateY,
    string Comment, FeedbackPinCategory Category);

public record RequestRevisionCommand(
    Guid SubmissionId,
    Guid ReviewerId,
    string ActorRole,
    string FeedbackMessage,
    List<FeedbackPinInput> Pins       // ← THÊM MỚI
) : IRequest<RequestRevisionResult>;

public record RequestRevisionResult(Guid SubmissionId, string NewStatus);
```

### Bước 4.2: Cập nhật Handler
```csharp
public async Task<RequestRevisionResult> Handle(RequestRevisionCommand cmd, CancellationToken ct)
{
    var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
        ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

    // 1. Archive các pins cũ (nếu có)
    var existingPins = await _repo.GetActivePinsBySubmissionIdAsync(cmd.SubmissionId, ct);
    foreach (var pin in existingPins) pin.Archive();

    // 2. Tạo pins mới
    var newPins = cmd.Pins.Select(p => SubmissionFeedbackPin.Create(
        submission.Id, p.PageIdentifier, p.CoordinateX, p.CoordinateY,
        p.Comment, p.Category, cmd.ReviewerId
    )).ToList();

    foreach (var pin in newPins) await _repo.AddPinAsync(pin, ct);

    // 3. Domain state transition
    submission.RequestRevision(cmd.ActorRole, cmd.ReviewerId, cmd.FeedbackMessage);

    await _repo.SaveChangesAsync(ct);

    return new RequestRevisionResult(submission.Id, submission.Status.ToString());
}
```

### Bước 4.3: Cập nhật Validator
```csharp
RuleFor(x => x.Pins).NotNull();
RuleForEach(x => x.Pins).ChildRules(pin =>
{
    pin.RuleFor(p => p.PageIdentifier).NotEmpty();
    pin.RuleFor(p => p.CoordinateX).InclusiveBetween(0, 100);
    pin.RuleFor(p => p.CoordinateY).InclusiveBetween(0, 100);
    pin.RuleFor(p => p.Comment).NotEmpty().MaximumLength(2000);
});
```

### Bước 4.4: Cập nhật Controller `eb-request-revision`
*   Tạo `RevisionWithPinsRequest` record nhận `Reason` + `List<FeedbackPinInput> Pins`.
*   Truyền pins vào command.

---

## Phase 5: Mở Rộng Notification Service (Deep-link & Aggregation)

### Bước 5.1: Thêm `TargetUrl` vào `NotificationConfiguration`
```csharp
// Trong NotificationConfiguration.Configure():
b.Property(e => e.TargetUrl).HasMaxLength(2048);
```
> Entity `Notification` đã có field `TargetUrl` — chỉ cần bổ sung mapping constraint.

### Bước 5.2: Mở rộng `INotificationService`
```csharp
// Thêm vào interface:
Task NotifySubmissionRevisionAsync(
    Guid receiverId, Guid submissionId, string message,
    int pinCount, string? targetUrl, CancellationToken ct = default);
```

### Bước 5.3: Implement trong `NotificationService`
```csharp
public async Task NotifySubmissionRevisionAsync(
    Guid receiverId, Guid submissionId, string message,
    int pinCount, string? targetUrl, CancellationToken ct = default)
{
    await _notificationRepo.AddAsync(new Notification
    {
        ReceiverId = receiverId,
        Title = $"Revision Required: {pinCount} issues found in your manuscript",
        Message = message,
        NotifyType = "SubmissionRevisionRequired",
        RelatedEntityId = submissionId,
        RelatedEntityType = "Submission",
        TargetUrl = targetUrl
    }, ct);
    await _notificationRepo.SaveChangesAsync(ct);

    // TODO Phase 6: SignalR push
}
```

### Bước 5.4: Gọi Notification từ `RequestRevisionHandler`
Sau `SaveChangesAsync`, gọi notification service:
```csharp
var firstPin = newPins.FirstOrDefault();
string targetUrl = $"/workspace/submissions/{submission.Id}/canvas";
if (firstPin != null)
    targetUrl += $"?page={Uri.EscapeDataString(firstPin.PageIdentifier)}&pinId={firstPin.Id}";

await _notificationService.NotifySubmissionRevisionAsync(
    submission.SubmitterId, submission.Id,
    $"Editorial Board pinned {newPins.Count} areas needing adjustments.",
    newPins.Count, targetUrl, ct);
```

> **Lưu ý Production:** Notification được gửi sau `SaveChangesAsync` thành công. Nếu cần đảm bảo 100% atomicity, chuyển sang Outbox Pattern (Phase 7).

---

## Phase 6: SignalR Real-time Push

### Bước 6.1: Cài NuGet Package (nếu chưa có)
```bash
# Kiểm tra trước — ASP.NET Core đã tích hợp sẵn SignalR
# Chỉ cần đảm bảo project reference Microsoft.AspNetCore.App
```

### Bước 6.2: Tạo `NotificationHub.cs`
File: `Shared/MangaERP.Shared.Infrastructure/Hubs/NotificationHub.cs`
```csharp
[Authorize]
public class NotificationHub : Hub
{
    // Hub rỗng — chỉ dùng để push từ server qua IHubContext
    // Client nhận event "ReceiveNotification"
}
```

### Bước 6.3: Đăng ký trong `Program.cs`
```csharp
builder.Services.AddSignalR();
// ...
app.MapHub<NotificationHub>("/hubs/notifications");
```

### Bước 6.4: Tích hợp SignalR vào `NotificationService`
```csharp
private readonly IHubContext<NotificationHub> _hubContext;

// Inject qua constructor, sau khi save DB:
await _hubContext.Clients.User(receiverId.ToString())
    .SendAsync("ReceiveNotification", new {
        title, message, targetUrl, submissionId, pinCount
    }, ct);
```

---

## Phase 7 (Tùy chọn): Nâng Cấp Production-Grade

Các bước này không bắt buộc cho MVP nhưng nên có cho production thực tế:

### 7.1: Domain Events + Outbox Pattern
*   Hiện tại `AggregateRoot` đã có `RaiseDomainEvent` và `DomainEvents` collection nhưng chưa có dispatcher.
*   Tạo `DomainEventDispatcher` interceptor trong `SaveChangesAsync` để tự động publish events sau commit.
*   Chuyển notification dispatch sang `INotificationHandler<SubmissionRevisionRequestedEvent>`.

### 7.2: SignalR Canvas Group Authorization
*   Thêm `JoinSubmissionCanvas(submissionId)` method vào Hub.
*   Validate quyền truy cập (chỉ SubmitterId hoặc EB/AssignedTE mới join được group).
*   Push notification theo group thay vì user khi cần collaborative canvas.

### 7.3: Query API cho Feedback Pins
*   `GET /api/v1/submissions/{id}/feedback-pins` — trả danh sách pins active (không archived).
*   `GET /api/v1/submissions/{id}/feedback-pins/history` — trả tất cả pins kèm revision round.
*   Thêm vào `SubmissionDetailDto`: `FeedbackPins` collection.

### 7.4: EF Migration & Data Integrity
*   Tạo migration sau khi hoàn thành Phase 3-5.
*   Kiểm tra data migration cho submissions cũ có status `Pending_TE_Review` trong DB production.
*   Thêm SQL migration: `UPDATE "SeriesSubmissions" SET "Status" = 'Pending_EB_Review' WHERE "Status" = 'Pending_TE_Review'`.

---

## Tóm Tắt Thứ Tự Triển Khai

| Phase | Mô tả | Files chính cần sửa | Độ ưu tiên |
|:---:|:---|:---|:---:|
| 1 | Dọn code TE 2 tầng | Controller, State Machine doc | **Cao** |
| 2 | AssignedEditorId khi Approve | ApproveSubmissionHandler, Controller | **Cao** |
| 3 | Entity FeedbackPin + EF Config | Domain, ModuleConfigurations, Migration | **Cao** |
| 4 | Refactor RequestRevisionHandler | Handler, Validator, Controller | **Cao** |
| 5 | Notification deep-link | INotificationService, NotificationService | **Cao** |
| 6 | SignalR real-time push | Hub, Program.cs, NotificationService | **Trung bình** |
| 7 | Production enhancements | Domain Events, Query API, Outbox | **Thấp** |
