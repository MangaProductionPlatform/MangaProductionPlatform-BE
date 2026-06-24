# Kế hoạch Chỉnh sửa BE – MF1 (Manga Feature 1)

> Trạng thái: **Chờ thực hiện** | Codebase: `MangaProductionPlatform-BE / src-monolith`

---

## Tổng quan các thay đổi

| # | Hạng mục | Files bị ảnh hưởng | Độ phức tạp |
|---|---|---|---|
| 1 | Bỏ `assignedEditorId` khỏi Approve, tự chọn TE | ApproveSubmissionHandler, Controller | Trung bình |
| 2 | Bỏ mâu thuẫn `ManagingTantouId` khi tạo Mangaka | ProvisionAccountHandler, AdminController | Thấp |
| 3 | Thêm `submitter` object vào SubmissionDetail | GetSubmissionDetailHandler | Thấp |
| 4 | Notification API cho Mangaka (GET + PATCH read) | Tạo mới NotificationsController + Handler | Trung bình |
| 5 | Sửa deep-link trong NotificationService | NotificationService.cs | Thấp |

---

## Chi tiết từng thay đổi

---

### #1 – Bỏ `assignedEditorId` khỏi Approve Request, System tự chọn TE

**Vấn đề:** EB phải gửi `assignedEditorId` (GUID cụ thể) trong body khi Approve. Yêu cầu mới: System tự tìm TE phù hợp.

**Chiến lược chọn TE tự động:**
- Lấy tất cả User có `Role = TantouEditor` và `AccountStatus = Active`
- Chọn TE có **ít Mangaka đang phụ trách nhất** (load balancing) — đếm `Users` có `ManagingTantouId = te.Id`
- Nếu không tìm được TE nào → throw `InvalidOperationException`

#### 1a. `ApproveSubmissionHandler.cs`

**Thay đổi Command:**
```diff
- public record ApproveSubmissionCommand(
-     Guid SubmissionId,
-     Guid ReviewerId,
-     Guid AssignedEditorId
- ) : IRequest<ApproveSubmissionResult>;
+ public record ApproveSubmissionCommand(
+     Guid SubmissionId,
+     Guid ReviewerId
+ ) : IRequest<ApproveSubmissionResult>;
```

**Thêm logic tự chọn TE trong Handle():**
```csharp
// Sau khi load submission, TRƯỚC khi mở transaction:

// ── Auto-select TantouEditor (least load) ────────────────────────────────
var allTE = await _userRepo.GetByRoleAsync(UserRole.TantouEditor, ct);
var activeTE = allTE.Where(u => u.AccountStatus == AccountStatus.Active).ToList();
if (!activeTE.Any())
    throw new InvalidOperationException("Không có Tantou Editor nào đang hoạt động để gán.");

// Load số Mangaka mỗi TE đang phụ trách
var allUsers = await _userRepo.GetAllAsync(ct);
var teWithLoad = activeTE
    .Select(te => new {
        Editor = te,
        Load = allUsers.Count(u => u.ManagingTantouId == te.Id)
    })
    .OrderBy(x => x.Load)
    .First();

var selectedTe = teWithLoad.Editor;
```

**Trong transaction, thay `cmd.AssignedEditorId` → `selectedTe.Id`:**
```diff
- mangaka.ManagingTantouId = cmd.AssignedEditorId;
+ mangaka.ManagingTantouId = selectedTe.Id;
```

**Result trả về:**
```diff
- result = new ApproveSubmissionResult(
-     submission.Id, series.Id, cmd.AssignedEditorId, ...);
+ result = new ApproveSubmissionResult(
+     submission.Id, series.Id, selectedTe.Id, ...);
```

**Sửa Validator (bỏ rule AssignedEditorId):**
```diff
- RuleFor(x => x.AssignedEditorId).NotEmpty()
-     .WithMessage("Phải chỉ định Tantou Editor phụ trách Series.");
```

#### 1b. `SubmissionsController.cs`

```diff
- public record ApproveRequest(Guid AssignedEditorId);

// Action Approve:
- var command = new ApproveSubmissionCommand(id, GetUserId(), request.AssignedEditorId);
+ var command = new ApproveSubmissionCommand(id, GetUserId());

// Action signature:
- public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveRequest request, ...)
+ public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
```

> [!NOTE]
> Cần thêm `IUserRepository` vào `ApproveSubmissionHandler` để query users có `ManagingTantouId`. Interface `IUserRepository.GetAllAsync()` đã tồn tại.

---

### #2 – Bỏ mâu thuẫn `ManagingTantouId` khi tạo Mangaka

**Vấn đề:** Admin tạo Mangaka đang cho phép gán `ManagingTantouId` ngay lập tức, nhưng MF1 nói TE chỉ được gán sau khi Proposal được approve.

**Quyết định chọn:** `Mangaka chưa có TE cho đến khi MF1 approved` — tức là **bỏ `ManagingTantouId`** khỏi Provision request.

#### 2a. `ProvisionAccountHandler.cs`

```diff
- public record ProvisionAccountCommand(
-     string FullName,
-     string PersonalEmail,
-     UserRole Role,
-     string? PhoneNumber = null,
-     Guid? ManagingTantouId = null
- ) : IRequest<ProvisionAccountResult>;
+ public record ProvisionAccountCommand(
+     string FullName,
+     string PersonalEmail,
+     UserRole Role,
+     string? PhoneNumber = null
+ ) : IRequest<ProvisionAccountResult>;
```

```diff
// Bỏ đoạn validate ManagingTantouId:
- if (request.Role == UserRole.Mangaka && request.ManagingTantouId.HasValue)
- {
-     var tantou = await _userRepo.GetByIdAsync(request.ManagingTantouId.Value, cancellationToken);
-     if (tantou == null || tantou.Role != UserRole.TantouEditor)
-         throw new InvalidOperationException("Biên tập viên phụ trách không hợp lệ hoặc không tồn tại.");
- }

// Bỏ ManagingTantouId khỏi User creation:
-     ManagingTantouId = request.Role == UserRole.Mangaka ? request.ManagingTantouId : null,
+     ManagingTantouId = null, // Gán sau khi Proposal được EB_Approved
```

#### 2b. `AdminController.cs`

```diff
- public record ProvisionAccountRequest(
-     string FullName,
-     string PersonalEmail,
-     UserRole Role,
-     string? PhoneNumber = null,
-     Guid? ManagingTantouId = null
- );
+ public record ProvisionAccountRequest(
+     string FullName,
+     string PersonalEmail,
+     UserRole Role,
+     string? PhoneNumber = null
+ );

// Action ProvisionAccount:
- var command = new ProvisionAccountCommand(
-     request.FullName, request.PersonalEmail, request.Role,
-     request.PhoneNumber, request.ManagingTantouId);
+ var command = new ProvisionAccountCommand(
+     request.FullName, request.PersonalEmail, request.Role, request.PhoneNumber);
```

> [!IMPORTANT]
> Tương tự, `UpdateAccountRequest` và `UpdateAccountCommand` cũng có `ManagingTantouId`. Nếu muốn Admin **vẫn có thể gán TE thủ công** sau này (Admin override), giữ nguyên `UpdateAccount` — chỉ bỏ khỏi `ProvisionAccount`. Nếu không → bỏ cả `UpdateAccount`. **Đề nghị: giữ lại trong `UpdateAccount`** để Admin có thể điều chỉnh thủ công khi cần.

---

### #3 – Thêm `submitter` object vào SubmissionDetail

**Vấn đề:** `SubmissionDetailDto` chỉ trả `SubmitterId` (GUID). FE cần thêm `fullName`, `penName`, `personalEmail`.

#### 3a. `GetSubmissionDetailHandler.cs`

**Thêm DTO nested:**
```csharp
public record SubmitterDto(
    Guid UserId,
    string? FullName,
    string? PenName,
    string? PersonalEmail);
```

**Thêm field vào `SubmissionDetailDto`:**
```diff
public record SubmissionDetailDto(
    Guid Id,
    string Title,
    ...
    Guid SubmitterId,
+   SubmitterDto? Submitter,
    string Status,
    ...);
```

**Inject `IUserRepository` vào Handler:**
```csharp
public class GetSubmissionDetailHandler
    : IRequestHandler<GetSubmissionDetailQuery, SubmissionDetailDto>
{
    private readonly ISubmissionRepository _repo;
    private readonly IUserRepository _userRepo; // NEW

    public GetSubmissionDetailHandler(
        ISubmissionRepository repo,
        IUserRepository userRepo) // NEW
    {
        _repo = repo;
        _userRepo = userRepo;
    }

    public async Task<SubmissionDetailDto> Handle(...)
    {
        var submission = await _repo.GetByIdAsync(...) ?? throw ...;
        // ownership check...

        var submitter = await _userRepo.GetByIdAsync(submission.SubmitterId, ct);
        var submitterDto = submitter is null ? null : new SubmitterDto(
            submitter.Id,
            submitter.FullName,
            submitter.PenName,
            submitter.PersonalEmail);

        return new SubmissionDetailDto(
            submission.Id,
            ...,
            submission.SubmitterId,
            submitterDto,    // NEW
            ...);
    }
}
```

> [!NOTE]
> `IUserRepository` đã được inject ở nhiều handler khác (e.g., `ApproveSubmissionHandler`). Không cần đăng ký thêm DI.
> File này nằm trong module `Submission` nhưng cần tham chiếu `MangaERP.Identity.Application.Ports` — đã có trong `.csproj` (cùng solution).

---

### #4 – Notification API cho Mangaka

**Vấn đề:** BE đã tạo `Notification` entity, lưu DB, push SignalR — nhưng thiếu REST API để FE poll/read.

#### 4a. Thêm method vào `INotificationRepository` (IPublishingPorts.cs)

```diff
public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Notification>> GetUnreadByReceiverAsync(Guid receiverId, CancellationToken ct = default);
+   Task<IEnumerable<Notification>> GetAllByReceiverAsync(Guid receiverId, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

#### 4b. Implement trong `PublishingRepositories.cs`

```csharp
async Task<IEnumerable<Notification>> INotificationRepository.GetAllByReceiverAsync(Guid receiverId, CancellationToken ct)
    => await _db.Notifications
        .Where(n => n.ReceiverId == receiverId)
        .OrderByDescending(n => n.CreatedAt)
        .ToListAsync(ct);
```

#### 4c. Tạo Queries trong Publishing (hoặc Submission module)

Tạo file mới: `MangaERP.Publishing/Application/Queries/GetMyNotifications/GetMyNotificationsHandler.cs`

```csharp
public record GetMyNotificationsQuery(
    Guid ReceiverId,
    bool UnreadOnly = false
) : IRequest<IEnumerable<NotificationDto>>;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    bool IsRead,
    string NotifyType,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    string? TargetUrl,
    DateTime CreatedAt);

public class GetMyNotificationsHandler
    : IRequestHandler<GetMyNotificationsQuery, IEnumerable<NotificationDto>>
{
    private readonly INotificationRepository _repo;
    public GetMyNotificationsHandler(INotificationRepository repo) => _repo = repo;

    public async Task<IEnumerable<NotificationDto>> Handle(
        GetMyNotificationsQuery query, CancellationToken ct)
    {
        var notifications = query.UnreadOnly
            ? await _repo.GetUnreadByReceiverAsync(query.ReceiverId, ct)
            : await _repo.GetAllByReceiverAsync(query.ReceiverId, ct);

        return notifications.Select(n => new NotificationDto(
            n.Id, n.Title, n.Message, n.IsRead,
            n.NotifyType, n.RelatedEntityId, n.RelatedEntityType,
            n.TargetUrl, n.CreatedAt));
    }
}
```

#### 4d. Tạo Command MarkNotificationRead

Tạo file mới: `MangaERP.Publishing/Application/Commands/MarkNotificationRead/MarkNotificationReadHandler.cs`

```csharp
public record MarkNotificationReadCommand(
    Guid NotificationId,
    Guid RequesterId
) : IRequest<bool>;

public class MarkNotificationReadHandler
    : IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationRepository _repo;
    public MarkNotificationReadHandler(INotificationRepository repo) => _repo = repo;

    public async Task<bool> Handle(MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        var notification = await _repo.GetByIdAsync(cmd.NotificationId, ct)
            ?? throw new KeyNotFoundException($"Notification {cmd.NotificationId} not found.");

        if (notification.ReceiverId != cmd.RequesterId)
            throw new UnauthorizedAccessException("Bạn không có quyền đánh dấu thông báo này.");

        notification.IsRead = true;
        await _repo.UpdateAsync(notification, ct);
        return true;
    }
}
```

#### 4e. Tạo `NotificationsController.cs`

Tạo file mới: `MangaERP.Publishing/Presentation/Controllers/NotificationsController.cs`

```csharp
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotificationsController(IMediator mediator) => _mediator = mediator;

    private Guid GetUserId() { ... } // same pattern as SubmissionsController

    /// <summary>
    /// [All roles] Get own notifications. ?unreadOnly=true to filter unread.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificationDto>), 200)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = new GetMyNotificationsQuery(GetUserId(), unreadOnly);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// [All roles] Mark a notification as read.
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new MarkNotificationReadCommand(id, GetUserId()), ct);
            return Ok(new { message = "Đã đánh dấu đã đọc." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }
}
```

> [!IMPORTANT]
> Cần đăng ký Publishing module handlers trong `PublishingModuleExtensions.cs` nếu chưa có `AddMediatR` scan assembly.

---

### #5 – Sửa deep-link trong NotificationService

**Vấn đề:** BE đang tạo URL `/workspace/...` nhưng FE dùng route khác.

#### `NotificationService.cs`

```diff
// NotifySubmissionApprovedAsync:
-   TargetUrl = $"/workspace/series/{seriesId}"
+   TargetUrl = $"/mangaka/series/{seriesId}"

// NotifySubmissionRejectedAsync:
-   TargetUrl = $"/workspace/submissions/{submissionId}"
+   TargetUrl = $"/mangaka/submissions"

// NotifySubmissionRevisionAsync (targetUrl được truyền từ ngoài vào):
// → Sửa ở handler gọi nó (RequestRevisionHandler):
-   targetUrl: $"/workspace/submissions/{submissionId}"
+   targetUrl: $"/mangaka/submissions"
```

> [!NOTE]
> `NotifySubmissionRevisionAsync` nhận `targetUrl` từ bên ngoài. Cần tìm nơi gọi (RequestRevisionHandler) và sửa giá trị được truyền vào.

---

## Thứ tự thực hiện đề xuất

```
1. #5 – Deep-link fix (5 phút, không có dependency)
2. #2 – Bỏ ManagingTantouId khỏi Provision (10 phút)
3. #3 – Thêm submitter object (15 phút)
4. #1 – Auto-assign TE khi Approve (20 phút)
5. #4 – Notification API (30 phút, cần tạo nhiều file mới)
```

---

## Files cần tạo mới

| File | Mô tả |
|---|---|
| `MangaERP.Publishing/Application/Queries/GetMyNotifications/GetMyNotificationsHandler.cs` | Query + DTO + Handler |
| `MangaERP.Publishing/Application/Commands/MarkNotificationRead/MarkNotificationReadHandler.cs` | Command + Handler |
| `MangaERP.Publishing/Presentation/Controllers/NotificationsController.cs` | REST endpoints |

## Files cần sửa

| File | Thay đổi |
|---|---|
| `ApproveSubmission/ApproveSubmissionHandler.cs` | Bỏ AssignedEditorId, thêm auto-select TE logic |
| `Submission/Presentation/Controllers/SubmissionsController.cs` | Bỏ ApproveRequest body |
| `Identity/Application/Commands/ProvisionAccount/ProvisionAccountHandler.cs` | Bỏ ManagingTantouId |
| `Identity/Presentation/Controllers/AdminController.cs` | Bỏ ManagingTantouId khỏi ProvisionAccountRequest |
| `Submission/Application/Queries/GetSubmissionDetail/GetSubmissionDetailHandler.cs` | Thêm SubmitterDto, inject IUserRepository |
| `Publishing/Application/Ports/IPublishingPorts.cs` | Thêm GetAllByReceiverAsync |
| `Shared/Infrastructure/Repositories/PublishingRepositories.cs` | Implement GetAllByReceiverAsync |
| `Shared/Infrastructure/Services/NotificationService.cs` | Sửa TargetUrl deep-links |

