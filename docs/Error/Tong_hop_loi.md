# Tổng Hợp Lỗi — MangaProductionPlatform-BE

> **Cập nhật lần cuối:** 2026-06-23  
> **Nhánh:** `bao`  
> **Môi trường:** Render (production) + Local

---

## LỖI 001 — Enum không deserialize được từ chuỗi JSON

### Thông tin
| Mục | Chi tiết |
|-----|---------|
| **Endpoint** | `POST /api/v1/submissions/{id}/request-revision` |
| **HTTP Status** | `400 Bad Request` |
| **Phát hiện** | 2026-06-23 |
| **Trạng thái** | ✅ Đã fix & deployed |

### Triệu chứng
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "request": ["The request field is required."],
    "$.pins[0].category": [
      "The JSON value could not be converted to MangaERP.Submission.Presentation.Controllers.RevisionPinRequest."
    ]
  }
}
```

### Nguyên nhân
ASP.NET Core `System.Text.Json` mặc định deserialize enum theo **số nguyên** (`0`, `1`, `2`), không hỗ trợ chuỗi (`"Content"`, `"Visual"`, `"Typo"`).  
Khi JSON body chứa `"category": "Content"` → binding fail → toàn bộ `request` object = null → lỗi phụ `"The request field is required."`.

**Enum bị ảnh hưởng:**
- `FeedbackPinCategory { Visual=0, Content=1, Typo=2 }` trong `RevisionPinRequest`

### Fix
**File:** `src-monolith/src/MangaERP.Api/Program.cs`

```csharp
// TRƯỚC (lỗi):
builder.Services.AddControllers();

// SAU (đã fix):
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
```

**Tác dụng:** API bây giờ nhận cả hai dạng:
- ✅ Chuỗi: `"category": "Content"`  
- ✅ Số nguyên: `"category": 1`  

**Phạm vi ảnh hưởng:** Toàn bộ API — tất cả enum trong request/response sẽ được serialize/deserialize theo tên chuỗi.

### Test lại sau fix
```json
// POST /api/v1/submissions/{id}/request-revision
{
  "reason": "Cốt truyện thiếu chiều sâu...",
  "pins": [
    {
      "pageIdentifier": "page-1",
      "coordinateX": 25.5,
      "coordinateY": 40.0,
      "comment": "Thiếu giới thiệu nhân vật chính.",
      "category": "Content"   // ← Giờ hoạt động
    }
  ]
}
```

**Expected response `200 OK`:**
```json
{
  "submissionId": "7efeb5b7-c283-421c-916c-66819b49f6a7",
  "newStatus": "Requires_Revision",
  "feedbackMessage": "Cốt truyện thiếu chiều sâu...",
  "pinCount": 1,
  "reviewedAt": "2026-06-23T..."
}
```

---

## LỖI 002 — Double SaveChanges trong INotificationRepository

### Thông tin
| Mục | Chi tiết |
|-----|---------|
| **Endpoint** | Mọi endpoint gọi `INotificationService` |
| **HTTP Status** | Không crash — lỗi logic ngầm |
| **Phát hiện** | 2026-06-23 (qua code review) |
| **Trạng thái** | ✅ Đã fix & deployed |

### Nguyên nhân
`PublishingRepositories.INotificationRepository.AddAsync()` đã tự gọi `SaveChangesAsync()` bên trong.  
Nhưng `NotificationService` cũng gọi `_notificationRepo.SaveChangesAsync()` sau đó → **2 lần commit** cho 1 notification.

### Fix
**File:** `src-monolith/src/Shared/MangaERP.Shared.Infrastructure/Repositories/PublishingRepositories.cs`

```csharp
// TRƯỚC (lỗi — tự save bên trong AddAsync):
async Task INotificationRepository.AddAsync(Notification notification, CancellationToken ct)
{
    await _db.Notifications.AddAsync(notification, ct);
    await _db.SaveChangesAsync(ct);  // ← Save lần 1
}

// SAU (đã fix — chỉ stage, caller tự save):
async Task INotificationRepository.AddAsync(Notification notification, CancellationToken ct)
    // Only stages the entity — caller is responsible for calling SaveChangesAsync.
    => await _db.Notifications.AddAsync(notification, ct);
```

---

## LỖI 003 — FluentValidation không chạy tự động

### Thông tin
| Mục | Chi tiết |
|-----|---------|
| **Ảnh hưởng** | Tất cả endpoints của Submission module |
| **Triệu chứng** | Input không hợp lệ không bị reject sớm → lỗi DB hoặc domain exception |
| **Phát hiện** | 2026-06-23 (qua code review) |
| **Trạng thái** | ✅ Đã fix & deployed |

### Nguyên nhân
FluentValidation validators được đăng ký với `AddValidatorsFromAssembly()` nhưng không có MediatR `IPipelineBehavior` để tự động gọi chúng.

### Fix
1. Tạo `ValidationBehavior<TRequest, TResponse>` trong `Shared.Application/Behaviors/`
2. Đăng ký vào `SubmissionModuleExtensions`:
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));  // ← THÊM
});
```
3. Thêm `catch (ValidationException)` vào tất cả endpoints của `SubmissionsController`

---

## LỖI 004 — Thiếu notification khi Approve / Reject submission

### Thông tin
| Mục | Chi tiết |
|-----|---------|
| **Endpoint** | `POST /approve`, `POST /reject` |
| **Triệu chứng** | Flow chạy OK nhưng Mangaka không nhận được thông báo |
| **Phát hiện** | 2026-06-23 (qua flow review) |
| **Trạng thái** | ✅ Đã fix & deployed |

### Nguyên nhân
`ApproveSubmissionHandler` và `RejectSubmissionHandler` không inject `INotificationService` và không gọi notification sau khi commit.

### Fix
- Thêm `NotifySubmissionApprovedAsync` và `NotifySubmissionRejectedAsync` vào `INotificationService`
- Implement trong `NotificationService` (lưu DB + push SignalR)
- Inject và gọi trong cả 2 handler sau khi commit

---

## Template ghi lỗi mới

```
## LỖI XXX — [Tên lỗi ngắn gọn]

### Thông tin
| Mục | Chi tiết |
|-----|---------|
| **Endpoint** | `METHOD /api/v1/...` |
| **HTTP Status** | `4xx / 5xx` |
| **Phát hiện** | YYYY-MM-DD |
| **Trạng thái** | 🔴 Chưa fix / ✅ Đã fix |

### Triệu chứng
[Response body lỗi thực tế]

### Nguyên nhân
[Giải thích kỹ thuật]

### Fix
[Code diff + file đã sửa]

### Test lại sau fix
[Request JSON + Expected response]
```
