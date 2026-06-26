# TÀI LIỆU ĐẶC TẢ LUỒNG THÔNG BÁO
# NOTIFICATION WORKFLOW SPECIFICATIONS

> **Phiên bản:** 1.1 — Giai đoạn 1 (Chưa tích hợp AI)
> **Ngày tạo:** 2026-06-26
> **Cập nhật lần cuối:** 2026-06-26 — Thêm API `PATCH /notifications/read-all`
> **Trạng thái:** ✅ Đã triển khai

---

## 1. GIAI ĐOẠN 1: KHI CHƯA TÍCH HỢP AI (HIỆN TẠI)

Luồng thông báo hoàn toàn phụ thuộc vào các **hành động (Event)** do con người hoặc logic trạng thái kích hoạt.

---

### 🔔 Mốc 1: Tác giả nộp bản thảo lần đầu / Nộp lại (Submit / Re-submit)

| Trường | Giá trị |
|---|---|
| **Sự kiện kích hoạt** | Mangaka gọi API `POST /{id}/submit` hoặc `POST /{id}/resubmit` |
| **Trạng thái chuyển** | `Draft` / `Requires_Revision` → `Pending_EB_Review` |
| **Đối tượng nhận** | Toàn bộ người dùng có Role **EDITORIAL_BOARD** |
| **Loại thông báo** | Push + In-app |

**Nội dung thông báo:**
```
"Bản thảo mới: [Tên_Bản_Thảo] vừa được Tác giả [Tên_Tác_Giả] nộp lên hệ thống.
Mời hội đồng vào đánh giá và bỏ phiếu!"
```

**Mục đích:** Kêu gọi các Editor vào hàng đợi (Vetting Queue) để làm việc ngay, tránh ngâm bản thảo của tác giả.

---

### 🔔 Mốc 2: Thành viên Hội đồng bỏ phiếu (Cast Vote)

| Trường | Giá trị |
|---|---|
| **Sự kiện kích hoạt** | Một Editor gọi API `POST /{id}/vote`. Số phiếu tăng nhưng **chưa đủ 3 phiếu** (1/3 hoặc 2/3) |
| **Đối tượng nhận** | Các thành viên còn lại trong **EDITORIAL_BOARD** chưa vote cho bản thảo này *(Optional — cấu hình được)* |
| **Loại thông báo** | In-app only |

**Nội dung thông báo:**
```
"Bản thảo [Tên_Bản_Thảo] đã nhận được phiếu bầu từ [Tên_Editor_A].
Hệ thống đang chờ thêm các phiếu bầu còn lại (Hiện tại: X/3)."
```

> ⚠️ **Lưu ý quan trọng:** Tuyệt đối **KHÔNG** bắn thông báo cho Tác giả ở mốc này để tránh lộ quy trình nội bộ.

---

### 🔔 Mốc 3: Hội đồng chốt kết quả tự động (Đủ ≥ 3 phiếu và đạt đồng thuận)

| Trường | Giá trị |
|---|---|
| **Sự kiện kích hoạt** | Hệ thống chạy Aggregation Logic, phát hiện đạt đa số (2/3 hoặc 3/3 phiếu trùng loại) |
| **Trạng thái chuyển** | `Pending_EB_Review` → `EB_Approved` / `EB_Rejected` / `Requires_Revision` |

#### Kịch bản phản hồi:

**✅ Trường hợp APPROVE:**

*Gửi Mangaka:*
```
"Chúc mừng! Bản thảo [Tên_Bản_Thảo] của bạn đã được phê duyệt thành công
và đưa vào sản xuất!"
```

*Gửi Tantou Editor (người được gán tự động qua thuật toán cân bằng tải):*
```
"Bạn được chỉ định phụ trách tác phẩm mới [Tên_Tác_Phẩm]
của Tác giả [Tên_Tác_Giả]. Vui lòng liên hệ để bắt đầu sản xuất."
```

**❌ Trường hợp REJECT / REVISION:**

*Gửi Mangaka:*
```
"Bản thảo [Tên_Bản_Thảo] của bạn có cập nhật mới từ Hội đồng.
[Yêu cầu chỉnh sửa / Từ chối]. Lý do: [FeedbackMessage tổng hợp]."
```

---

### 🔔 Mốc 4: Xảy ra tranh chấp hệ thống (Đủ 3 phiếu nhưng kết quả là 1-1-1)

| Trường | Giá trị |
|---|---|
| **Sự kiện kích hoạt** | Aggregation Logic phát hiện 3 phiếu đá nhau |
| **Trạng thái chuyển** | `Pending_EB_Review` → `Conflict_Escalated` |
| **Đối tượng nhận** | Tất cả người dùng có Role **EDITOR_IN_CHIEF** |
| **Loại thông báo** | Push khẩn cấp (`urgent = true`) |

**Nội dung thông báo:**
```
"⚠️ CẢNH BÁO TRANH CHẤP: Bản thảo [Tên_Bản_Thảo] của Tác giả [Tên_Tác_Giả]
bất phân thắng bại (1-1-1) sau khi hội đồng bỏ phiếu.
Mời Tổng biên tập vào phân xử!"
```

> ⚠️ **Lưu ý quan trọng:** Tuyệt đối **KHÔNG** bắn thông báo cho Tác giả. Trên UI của tác giả vẫn hiển thị "Đang chờ duyệt" để họ không hoang mang.

---

### 🔔 Mốc 5: Tổng biên tập ra phán quyết cuối cùng

| Trường | Giá trị |
|---|---|
| **Sự kiện kích hoạt** | Editor-in-Chief gọi API `POST /{id}/resolve-conflict` |
| **Đối tượng nhận** | Mangaka (và Tantou Editor nếu kết quả là Approve) |

**Nội dung thông báo:** Tương tự nội dung ở **Mốc 3** — gửi kết quả kèm thông điệp / phán quyết từ Tổng biên tập.

---

## 2. GIAI ĐOẠN 2: KHI TÍCH HỢP COMPUTER VISION / AI (TƯƠNG LAI)

Khi tích hợp YOLO / SAM / U-Net, luồng thông báo sẽ chuyển dịch từ:

> **"Hành động của người → Bắn thông báo"**

sang:

> **"Hành động của người → Chờ AI quét xong → Bắn thông báo kèm dữ liệu AI"**

---

### Sự thay đổi tại Mốc Khởi Đầu (Khi Mangaka bấm Submit)

Hệ thống áp dụng kiến trúc **Bất đồng bộ (Asynchronous Background Job via Message Queue)** để hoãn thông báo cho đến khi AI chuẩn bị xong dữ liệu "gợi ý lỗi" cho con người.

```
[Mangaka bấm Submit]
       │
       ▼ (Chuyển trạng thái sang Pending_EB_Review)
[Hệ thống tạo một Background Job, đẩy bản thảo vào AI Worker xử lý]
       │
       ├─► [YOLO]: Quét vi phạm 18+, bạo lực, check ô thoại trống (1-2 phút)
       ├─► [U-Net/SAM]: Phân tách lớp nhân vật, tính toán độ chi tiết nét vẽ
       │
       ▼ (AI Worker hoàn thành, ghi nhận kết quả vào bảng SubmissionAiAnalysis)
[KÍCH HOẠT THÔNG BÁO CHO EDITORIAL BOARD]
```

---

### Chi tiết thay đổi các thông báo AI-Enhanced

#### Kịch bản A: AI làm trợ lý nội bộ *(Khuyên dùng)*

**Đối tượng nhận:** `EDITORIAL_BOARD`

**Nội dung thông báo mới:**
```
"Bản thảo mới [Tên_Bản_Thảo] đã quét xong AI thành công.
Phát hiện [X] điểm rủi ro định dạng/chính sách.
Mời hội đồng vào hàng đợi duyệt!"
```

**Ưu điểm:** Khi Editor nhận thông báo và click vào xem chi tiết, giao diện canvas đã hiển thị sẵn các **Bounding Box / Vùng lỗi** do AI khoanh vùng, giúp chấm điểm cực nhanh mà không phải chờ đợi.

---

#### Kịch bản B: AI làm người gác cổng tự động (AI Guardrail)

Nếu AI quét ra lỗi vi phạm nghiêm trọng với **Confidence Score > 95%**
*(Ví dụ: file ảnh hỏng, trang trắng hoàn toàn, vi phạm chính sách nặng)*:

**Hành động tự động:**
- Hệ thống tự động chuyển trạng thái bản thảo sang `Requires_Revision` ngay lập tức
- **HỦY** việc đưa vào hàng đợi của người thật

**Thông báo gửi cho Mangaka:**
```
"Hệ thống tự động phát hiện bản thảo [Tên_Bản_Thảo] của bạn không đạt
chuẩn kỹ thuật tại [Trang X] (Lỗi: Ô thoại trống / Ảnh lỗi).
Vui lòng kiểm tra và sửa đổi trước khi gửi lại cho Hội đồng."
```

---

## 3. KHUYẾN NGHỊ KIẾN TRÚC CODE (EVENT-DRIVEN PATTERN)

Để chuẩn bị cho tích hợp AI ở Giai đoạn 2 **mà không phải sửa lại code thông báo của Giai đoạn 1**, áp dụng pattern **Publish-Subscribe (Pub/Sub)**:

### Giai đoạn 1 (Hiện tại)
```csharp
// Handler Submit/ReSubmit: gọi trực tiếp sau khi commit
await _notificationService.NotifyNewSubmissionToEditorialBoardAsync(
    submissionId, title, authorName, ct);
```

### Giai đoạn 2 (Có AI) — Chỉ cần thay 1 dòng
```csharp
// Handler Submit/ReSubmit: publish event thay vì gọi trực tiếp
_eventBus.Publish(new SubmissionSubmittedEvent(submission.Id));

// AiJobHandler lắng nghe SubmissionSubmittedEvent:
//   → Chạy AI Worker (YOLO, U-Net, SAM)
//   → Ghi kết quả vào SubmissionAiAnalysis
//   → Publish AiScanningCompletedEvent

// AiNotificationHandler lắng nghe AiScanningCompletedEvent:
//   → Gọi NotifyNewSubmissionToEditorialBoardAsync (kèm dữ liệu AI)
```

### Sơ đồ chuyển đổi

```
──── GIAI ĐOẠN 1 ────────────────────────────────────────────
Submit → NotifyEB (trực tiếp)

──── GIAI ĐOẠN 2 ────────────────────────────────────────────
Submit → Publish(SubmissionSubmittedEvent)
              │
              └─► AiJobHandler
                        │ (AI xử lý xong)
                        └─► Publish(AiScanningCompletedEvent)
                                    │
                                    └─► AiNotificationHandler
                                                │
                                                └─► NotifyEB (kèm AI data)
```

---

## 4. TRIỂN KHAI THỰC TẾ (GIAI ĐOẠN 1)

### Files đã thay đổi

| File | Thay đổi |
|---|---|
| `Shared.Application/Ports/INotificationService.cs` | Thêm 4 phương thức mới |
| `Shared.Infrastructure/Services/NotificationService.cs` | Implement 4 phương thức mới |
| `Commands/SubmitProposal/SubmitProposalHandler.cs` | Wire Mốc 1 |
| `Commands/ReSubmitProposal/ReSubmitProposalHandler.cs` | Wire Mốc 1 |
| `Commands/CastVote/CastVoteHandler.cs` | Wire Mốc 2, 3 (approve path), Mốc 4 |
| `Commands/ApproveSubmission/ApproveSubmissionHandler.cs` | Wire Mốc 3 (Admin path) |
| `Commands/ResolveConflict/ResolveConflictHandler.cs` | Wire Mốc 5 |
| `Publishing/Ports/IPublishingPorts.cs` | Thêm `MarkAllAsReadAsync` vào interface |
| `Shared.Infrastructure/Repositories/PublishingRepositories.cs` | Implement `MarkAllAsReadAsync` bằng `ExecuteUpdateAsync` |
| `Commands/MarkAllNotificationsRead/` *(mới)* | Handler MediatR cho "đọc tất cả" |
| `Publishing/Controllers/NotificationsController.cs` | Thêm endpoint `PATCH /read-all` |

### NotifyType Registry

| `NotifyType` | Người nhận | Mốc tương ứng |
|---|---|---|
| `NewSubmissionPendingReview` | Tất cả EditorialBoard | 1 |
| `SubmissionVoteCast` | EB members chưa vote | 2 |
| `SubmissionApproved` | Mangaka | 3 / 5 |
| `TantouEditorAssigned` | Tantou Editor | 3 / 5 |
| `SubmissionConflictEscalated` | Tất cả EditorInChief | 4 |
| `SubmissionRejected` | Mangaka | 3 / 5 |
| `SubmissionRevisionRequired` | Mangaka | 3 / 5 |

### Kênh phân phối

Mỗi thông báo đều được gửi qua **2 kênh song song**:
1. **Database** — lưu vào bảng `Notifications` (In-app, persistent)
2. **SignalR** — push realtime qua `NotificationHub` → client `ReceiveNotification`

---

## 5. FRONTEND INTEGRATION APIs

Toàn bộ API frontend cần tích hợp để hiển thị thông báo:

### REST Endpoints

| Method | Endpoint | Mô tả | Auth |
|---|---|---|---|
| `GET` | `/api/v1/notifications` | Lấy tất cả thông báo của user hiện tại | Bearer JWT |
| `GET` | `/api/v1/notifications?unreadOnly=true` | Chỉ lấy thông báo chưa đọc | Bearer JWT |
| `PATCH` | `/api/v1/notifications/{id}/read` | Đánh dấu 1 thông báo đã đọc | Bearer JWT |
| `PATCH` | `/api/v1/notifications/read-all` | Đánh dấu **tất cả** chưa đọc là đã đọc | Bearer JWT |

### Response Schema — `GET /notifications`

```json
[
  {
    "id": "guid",
    "title": "Bản thảo mới chờ duyệt",
    "message": "Bản thảo \"One Piece\" vừa được Tác giả Oda nộp...",
    "isRead": false,
    "notifyType": "NewSubmissionPendingReview",
    "relatedEntityId": "submission-guid",
    "relatedEntityType": "Submission",
    "targetUrl": "/editorial/queue",
    "createdAt": "2026-06-26T03:00:00Z"
  }
]
```

### Response Schema — `PATCH /notifications/read-all`

```json
{
  "message": "Đã đánh dấu đọc 5 thông báo.",
  "updatedCount": 5
}
```

> **Lưu ý kỹ thuật:** `PATCH /read-all` thực thi **1 câu SQL `UPDATE` duy nhất** (không loop từng record), an toàn dùng ngay cả khi user có hàng trăm thông báo.

### SignalR Real-time

```
Endpoint: wss://{api-host}/hubs/notifications
Auth:     Truyền JWT qua query string: ?access_token={token}
Event:    "ReceiveNotification"
```

**Payload SignalR** (tương tự REST response + thêm trường context-specific):

```json
{
  "id": "guid",
  "title": "string",
  "message": "string",
  "notifyType": "string",
  "submissionId": "guid",       // nếu liên quan Submission
  "seriesId": "guid",           // nếu approve
  "urgent": true,               // chỉ có khi conflict escalated
  "targetUrl": "string",
  "createdAt": "ISO8601"
}
```

### Workflow tích hợp đề xuất cho Frontend

```
1. Login thành công
   └─► Kết nối SignalR hub với Bearer token
   └─► GET /notifications?unreadOnly=true → hiển thị badge count

2. Nhận "ReceiveNotification" từ SignalR
   └─► Append notification vào list
   └─► badge count += 1
   └─► Nếu urgent == true → hiển thị alert/toast đặc biệt

3. User mở notification panel
   └─► GET /notifications (load full list)

4. User click vào 1 thông báo
   └─► PATCH /notifications/{id}/read
   └─► Navigate tới targetUrl

5. User click "Đọc tất cả"
   └─► PATCH /notifications/read-all
   └─► Nhận updatedCount → reset badge về 0
```

### Điều hướng theo NotifyType

| `notifyType` | `targetUrl` gợi ý | Vai trò |
|---|---|---|
| `NewSubmissionPendingReview` | `/editorial/queue` | EditorialBoard |
| `SubmissionVoteCast` | `/editorial/queue` | EditorialBoard |
| `SubmissionApproved` | `/mangaka/series/{seriesId}` | Mangaka |
| `SubmissionRejected` | `/mangaka/submissions` | Mangaka |
| `SubmissionRevisionRequired` | `/mangaka/submissions` | Mangaka |
| `SubmissionConflictEscalated` | `/eic/conflict/{submissionId}` | EditorInChief |
| `TantouEditorAssigned` | `/te/dashboard` | TantouEditor |
