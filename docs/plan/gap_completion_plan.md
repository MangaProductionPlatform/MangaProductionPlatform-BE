# 🗺️ MangaERP — Kế Hoạch Hoàn Thiện Hệ Thống

> **Nguồn:** Tổng hợp từ `MangaERP_Tong_Hop_Gap_Analysis.md` + `MangaERP_Tong_Hop_Gap_Analysis (1).md`
> **Cập nhật lần cuối:** 2026-06-30 | **Mục tiêu:** Phân rõ việc cần làm để **demo được** vs **deploy production được**

---

## 📌 Changelog — Tiến Độ Thực Tế

| Ngày | PR / Người | Nội dung | Ghi chú |
|---|---|---|---|
| 2026-06-30 | PR #18 `bach-v2` | `SharedInfrastructureExtensions`: hỗ trợ URI format cho PostgreSQL connection string khi deploy | Không ảnh hưởng local dev |
| 2026-06-30 | PR #19 `nam1` | **3 features mới** (xem chi tiết bên dưới) — merge vào main | ✅ Đã pull về local |

### ✅ Hoàn thành trong PR #19 `nam1`

| Feature | API | File | Trạng thái |
|---|---|---|---|
| Giao nhiều trang cho Assistant cùng lúc | `POST /api/v1/chapters/{id}/pages/bulk-activate` | `BulkActivatePageTasksHandler.cs` | ✅ Đã merge |
| Duyệt hàng loạt layer của Assistant | `POST /api/v1/tasks/bulk-review` | `BulkReviewLayersHandler.cs` | ✅ Đã merge |
| Xem lịch sử phiên bản layer | `GET /api/v1/tasks/{pageTaskId}/layers` | `GetLayerHistoryHandler.cs` | ✅ Đã merge |

> **Tác động lên plan:** Mục B3 — `GET /api/v1/tasks/{pageTaskId}/layers` đã hoàn thành.

---

## 🐛 Bugs Cần Fix — Phát Hiện Từ Code Review PR #19

> ⚠️ **Assign cho đúng người trước khi làm phase tiếp theo.**

### Bug #1 — `BulkReviewLayersHandler.cs` thiếu `SaveChangesAsync` cho PageTask [NGHIÊM TRỌNG]

- **File:** `MangaERP.Task/Application/Commands/BulkReviewLayers/BulkReviewLayersHandler.cs`
- **Dòng:** ~113
- **Vấn đề:** Chỉ gọi `_layerRepo.SaveChangesAsync(ct)` nhưng **thiếu** `_pageTaskRepo.SaveChangesAsync(ct)`. Trạng thái `pageTask.Accept()` / `pageTask.RequestRevision()` có thể không được persist vào DB nếu hai repo dùng DbContext riêng.
- **Hậu quả:** Mangaka duyệt layer nhưng task status không thay đổi trong DB → FE hiển thị sai trạng thái.
- **Fix:** Thêm `await _pageTaskRepo.SaveChangesAsync(ct);` sau dòng `_layerRepo.SaveChangesAsync`.
- **Người fix:** _(chưa assign)_
- **Trạng thái:** 🔴 Chưa fix

### Bug #2 — `GetLayerHistoryHandler.cs` xác định Status sai logic [NGHIÊM TRỌNG]

- **File:** `MangaERP.Task/Application/Queries/GetLayerHistory/GetLayerHistoryHandler.cs`
- **Dòng:** ~103
- **Vấn đề:** `string statusStr = layer.RejectionNote == null ? "Accepted" : "Rejected"` — suy luận status từ `RejectionNote` thay vì dùng field/enum thật trên entity. Nếu layer có `RejectionNote` cũ nhưng sau đó được Accept → hiển thị sai là "Rejected".
- **Fix:** Dùng field `Status` hoặc enum có sẵn trên entity `ArtworkLayer` thay vì suy luận từ `RejectionNote`.
- **Người fix:** _(chưa assign)_
- **Trạng thái:** 🔴 Chưa fix

### Issue #3 — `GetLayerHistoryHandler.cs` filter `?status=Pending` trả về rỗng không rõ lý do [UX]

- **File:** `MangaERP.Task/Application/Queries/GetLayerHistory/GetLayerHistoryHandler.cs`
- **Dòng:** ~113–114
- **Vấn đề:** `ValidStatuses` bao gồm `"Pending"` nhưng handler lại skip toàn bộ Pending layers (vì `ReviewedAt == null`) → FE gọi `?status=Pending` luôn nhận `[]` mà không có thông báo lỗi.
- **Fix (2 lựa chọn):**
  - Bỏ `"Pending"` khỏi `ValidStatuses` trong Validator, trả `400 Bad Request` nếu FE gửi lên.
  - Hoặc: Hỗ trợ Pending thật sự bằng cách không filter `ReviewedAt != null` khi status là Pending.
- **Người fix:** _(chưa assign)_
- **Trạng thái:** 🟡 Minor — nhưng nên fix trước khi FE tích hợp

### Minor #4 — `BulkActivatePageTasksHandler.cs` N+1 queries [PERFORMANCE]

- **File:** `MangaERP.Chapter/Application/Commands/BulkActivatePageTasks/BulkActivatePageTasksHandler.cs`
- **Dòng:** ~59–65
- **Vấn đề:** Loop gọi `GetByChapterAndPageNumberAsync` từng trang một → 20 pages = 20 queries DB.
- **Fix:** Thêm method `GetByChapterAndPageNumbersAsync(chapterId, pageNumbers[])` vào `IPageTaskRepository` để lấy toàn bộ trong 1 query.
- **Người fix:** _(chưa assign)_
- **Trạng thái:** 🟢 Có thể để sau, ưu tiên thấp hơn Bug #1 và #2

---

---

## ⚡ TL;DR — Hai loại GAP, hai mục tiêu khác nhau

| | GAP A — FE ↔ BE | GAP B — System Completeness |
|---|---|---|
| **Mục tiêu** | Các trang FE hết trắng, demo được | Hệ thống chạy thật, người dùng không bị kẹt |
| **Dấu hiệu thiếu** | Trang hiển thị `EmptyBackendState` | Không có trang nào báo lỗi, nhưng user thật bị stuck |
| **Ưu tiên** | 🔴 Làm trước | ⚪ Bắt buộc trước go-live |

**Ký hiệu ưu tiên:** 🔴 Cần ngay | 🟡 Quan trọng | 🟢 Nên có | ⚪ Hạ tầng/go-live

---

## 👤 Phân Chia Ownership — Quy Tắc Bắt Buộc Khi Vibe Coding

> ⚠️ **Mỗi thành viên chỉ được commit vào các file thuộc mainflow mình phụ trách.**
> Nếu cần sửa file của người khác, phải báo qua team chat và đợi confirm trước — tránh conflict khi cả nhóm vibe coding song song.

| Mainflow | Người phụ trách BE | Module / Thư mục thuộc phạm vi |
|---|---|---|
| **MF1** — Series Proposal & Vetting | **Bao** branch `bao`| `MangaERP.Submission`, `MangaERP.Series` (cancellation flow) |
| **MF2** — Chapter Production & Task | **Nam** (branch `nam1`) | `MangaERP.Chapter`, `MangaERP.Task`, `MangaERP.Studio` |
| **MF3** — QA & Publishing | **Bach** `bach-v2`| `MangaERP.QA`, `MangaERP.Publishing` |
| **Core - Phần 1** (Identity, Security & Notifications) | **Nam** (branch `nam1`) | `MangaERP.Identity` (Auth/Profile, Notifications, SignalR, Email, RateLimiter) |
| **Core - Phần 2** (Ranking & Background Jobs) | **Bach** (branch `bach-v2`) | `MangaERP.Ranking`, Quartz/Hangfire Scheduled Publisher Job |
| **Core - Phần 3** (Admin Portal & Global Infrastructure) | **Bao** (branch `bao`) | `MangaERP.Identity` (Admin endpoints), S3 Storage, Middlewares (Exception, Audit) |

**Quy tắc file cụ thể:**
- Không ai được sửa `SharedInfrastructureExtensions.cs` hoặc `AppDbContext.cs` mà không báo cả nhóm trước (file shared dùng chung).
- Migration EF Core: người nào thêm entity/column thì người đó tạo migration, không ai tự thêm migration cho module khác.
- Controller của module nào thì chỉ người phụ trách module đó được sửa.

---

# 🔴 ƯU TIÊN CAO NHẤT — FE Cần Cập Nhật Luồng Auth (Cookie-based)

> **Backend Identity đã thay đổi cơ chế lưu refreshToken.** FE hiện tại KHÔNG tương thích. Phải fix trước tất cả mọi thứ khác vì ảnh hưởng đến mọi API call sau khi login.

## Vấn đề hiện tại

Backend (`AuthController.cs`) trả về:
- **`accessToken`** → trong response body ✅
- **`refreshToken`** → trong **httpOnly cookie** (KHÔNG còn trong body) ❌

FE hiện tại (`mangaErpService.ts` dòng 63) đang cố đọc `refreshToken` từ body:
```typescript
refreshToken: pick<string>(data, "refreshToken"), // ❌ sẽ trả về undefined
```

Và type `CurrentUser` (`mangaErp.ts` dòng 24) vẫn có field `refreshToken: string` — không còn ý nghĩa.

Ngoài ra `httpClient.ts` **không có** `credentials: "include"` → browser sẽ KHÔNG tự gửi cookie khi gọi `/api/v1/auth/refresh` → silent refresh thất bại.

## Việc FE cần làm (tất cả trong cùng 1 PR)

### 1. `mangaErpService.ts` — Sửa hàm `login()`
```typescript
// Xóa dòng refreshToken khỏi return object
// BE đã set httpOnly cookie tự động — FE không cần lưu
return {
  email,
  userId: pick<string>(data, "userId"),
  role:   normalizeRole(pick<string>(data, "role")),
  accessToken: pick<string>(data, "accessToken"),
  // refreshToken: KHÔNG lấy từ body nữa
};
```

### 2. `mangaErp.ts` — Xóa field `refreshToken` khỏi type `CurrentUser`
```typescript
// Trước
export type CurrentUser = { ...; refreshToken: string; ... }
// Sau
export type CurrentUser = { ...; /* refreshToken đã xóa */ ... }
```

### 3. `httpClient.ts` — Thêm `credentials: "include"` vào mọi request
```typescript
const response = await fetch(`${SERVICE_BASE_URLS[service]}${path}`, {
  ...init,
  headers,
  credentials: "include", // ← Bắt buộc để browser gửi httpOnly cookie
});
```

### 4. `mangaErpService.ts` — Thêm hàm `refresh()` và `logout()` đúng
```typescript
async refresh(): Promise<{ accessToken: string }> {
  // Không cần body — browser tự gửi cookie nhờ credentials: "include"
  return request<{ accessToken: string }>("identity", "/api/v1/auth/refresh", {
    method: "POST",
  });
},

async logout(): Promise<void> {
  await request<void>("identity", "/api/v1/auth/logout", { method: "POST" });
  // Cookie bị xóa ở BE; FE xóa localStorage
  clearAuthSession();
},
```

### 5. `authSession.ts` — Xóa thêm `refreshToken` khỏi localStorage nếu có
```typescript
export function clearAuthSession() {
  localStorage.removeItem("currentUser");
  // refreshToken không còn trong localStorage, nhưng clear cho an toàn
}
```

### 6. `LoginPage.tsx` — Không lưu `refreshToken` vào localStorage nữa
```typescript
// Chỉ lưu { email, userId, role, accessToken }
localStorage.setItem("currentUser", JSON.stringify(account));
```

## Tóm tắt tác động

| File | Thay đổi | Người làm |
|---|---|---|
| `mangaErpService.ts` | Xóa `refreshToken` khỏi `login()`, thêm `refresh()` + `logout()` | _(chưa assign)_ |
| `mangaErp.ts` | Xóa field `refreshToken` khỏi type `CurrentUser` | _(chưa assign)_ |
| `httpClient.ts` | Thêm `credentials: "include"` | _(chưa assign)_ |
| `authSession.ts` | Đảm bảo không clear gì liên quan cookie | _(chưa assign)_ |
| `LoginPage.tsx` | Không lưu `refreshToken` vào localStorage | _(chưa assign)_ |

---

# PHẦN A — GAP: Frontend ↔ Backend (Đủ để Demo)

> Mục tiêu: Xóa hết các trang đang hiển thị `EmptyBackendState`.

---

## MF1 — Series Proposal & Vetting `👤 _(chưa assign)_` (Duyệt Đề Xuất Truyện)

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)

| Trang FE | API đã có | Việc FE cần làm |
|---|---|---|
| Board Voting Center | `GET /api/v1/submissions/queue` | Hợp nhất route `/app/board/voting-center` → `SeriesProposalsPage.tsx` |
| Board Dashboard | `POST /api/v1/submissions/{id}/vote`<br>`POST /api/v1/submissions/{id}/resolve-conflict` | Wire API vote và resolve vào component |

### A — API thiếu cần viết mới

| Method | Route | Trang FE chờ | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/series/cancellation-queue` | `CancellationReviewPage.tsx` | 🟡 |
| GET | `/api/v1/board/reports` | `ReportsPage.tsx` (Board EB/EIC) | 🟡 |

---

## MF2 — Chapter Production & Task Assignment `👤 Nam`

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)

| Trang FE | API đã có | Việc FE cần làm |
|---|---|---|
| Editor Dashboard (Tantou) | `GET /api/v1/series` (filter `ManagingTantouId` từ JWT)<br>`GET /api/v1/chapters?seriesId=...` | Thay mock data bằng API thật |
| `SeriesMonitoringPage.tsx` (Editor) | `GET /api/v1/series` | Như trên |
| Assistant Pages | `GET /api/v1/tasks/assigned`<br>`POST /api/v1/tasks/{id}/submit-layer` | Đã đủ — chỉ cần FE wire vào |

> ⚠️ **Riêng `AdminSeriesMonitoringPage.tsx`:** BE bổ sung thêm quyền Admin xem toàn bộ (không filter userId) + query param `?status=Active|Cancelled|Hiatus`.

### A — API thiếu cần viết mới

| Method | Route | Trang FE chờ | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/chapters/my-queue` | `ReviewQueuePage.tsx` (Tantou Editor) | 🔴 |
| GET | `/api/v1/assistant/tasks/income` | `AssistantIncomePage.tsx` | 🟢 |

---

## MF3 — Quality Assurance & Publishing `👤 _(chưa assign)_`

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)

| Trang FE | API đã có | Việc FE cần làm |
|---|---|---|
| `ReviewQueuePage.tsx` (Editor/QA) | `GET /api/v1/qa/chapters/{id}/pins`<br>`GET /api/v1/qa/chapters/{id}/session`<br>`POST /api/v1/qa/chapters/{id}/pins`<br>`POST /api/v1/qa/chapters/{id}/send-feedback` | Thêm QA calls vào service layer, dựng canvas ghim lỗi |

### A — API thiếu cần viết mới

| Method | Route | Trang FE chờ | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/publishing/chapters/my-queue` | `PublishingQueuePage.tsx` (Tantou Editor) | 🔴 |
| GET | `/api/v1/publishing/schedule` | `PublishingSchedulePage.tsx` | 🟡 |

---

## Core — Admin, Notifications & Ranking (Chia theo Owner)

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)
> **Người phụ trách:** **Nam** (Core - Phần 1) tích hợp SignalR; cả team wire API vào các dashboard tương ứng.

| Trang FE | API đã có | Việc FE cần làm |
|---|---|---|
| `AdminNotificationsPage`<br>`BoardNotificationsPage`<br>`editors/NotificationsPage` | `GET /api/v1/notifications`<br>`PATCH /api/v1/notifications/{id}/read`<br>`PATCH /api/v1/notifications/read-all` | Xóa `EmptyBackendState`, thêm call vào `mangaErpService.ts`, render thật; sau đó kết nối SignalR `/hubs/notifications` |

### A — API thiếu cần viết mới

| Method | Route | Trang FE chờ | Ưu tiên | Người làm BE |
|---|---|---|---|---|
| GET | `/api/v1/admin/dashboard` | `AdminDashboardPage.tsx` | 🔴 | **Bao** (Core - Phần 3) |
| GET | `/api/v1/admin/workflow-stats` | `AdminWorkflowMonitoringPage.tsx` | 🟡 | **Bao** (Core - Phần 3) |
| GET | `/api/v1/admin/reports` | `AdminReportsAnalyticsPage.tsx` | 🟡 | **Bao** (Core - Phần 3) |
| GET | `/api/v1/admin/roles` | `AdminRolesPage.tsx` | 🟢 | **Bao** (Core - Phần 3) |

### A — Ranking Module (Khối lượng lớn nhất — toàn bộ cần viết mới)

> Hiện chỉ có Domain entities (`VoteData`, `RankingSnapshot`). Chưa có Application / Infrastructure / Controller.
> Trang FE chờ: `/ranking`, `RankingAnalyticsPage.tsx`, `RankingReportsPage.tsx`
> **Người phụ trách:** **Bach** (Core - Phần 2)

| Method | Route | Vai trò | Mô tả |
|---|---|---|---|
| POST | `/api/v1/ranking/import` | Admin, EB | Import phiếu bầu thô theo kỳ |
| POST | `/api/v1/ranking/compile` | Admin, EB | Gom vote, gán Rank, lưu Snapshot |
| GET | `/api/v1/ranking/board?period=...` | Public | Bảng xếp hạng chính thức |
| GET | `/api/v1/ranking/periods` | Public | Danh sách kỳ đã có snapshot |
| GET | `/api/v1/ranking/import/{period}` | Admin, EB | Xem phiếu thô đã import |
| DELETE | `/api/v1/ranking/import/{period}` | Admin | Xóa phiếu thô trước khi compile |

**Checklist BE Ranking (Phương án A — thủ công):**
- [ ] Infrastructure: `IVoteDataRepository`, `IRankingSnapshotRepository`, EF mapping, migration (**Bach**)
- [ ] Application: `ImportVoteDataCommand`, `CompileRankingCommand`, `GetRankingBoardQuery` (**Bach**)
- [ ] Presentation: `RankingController.cs` (**Bach**)

---

# PHẦN B — GAP: System Completeness (Production-Ready)

> Mục tiêu: Người dùng thật không bị kẹt, không mất dữ liệu, không lỗi bất ngờ.

---

## MF1 — Series Proposal & Vetting `👤 _(chưa assign)_`

### B — Luồng Cancellation (Thiếu hoàn toàn — có method `Cancel()` nhưng sai phân quyền)

> ⚠️ Quyết định hủy truyện thuộc **EB/EIC**, không phải Admin.

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|---|
| POST | `/api/v1/series/{id}/request-cancellation` | Mangaka | Luồng hủy đứt ngay bước đầu — Mangaka không gửi được | 🟡 |
| POST | `/api/v1/series/{id}/approve-cancellation` | EIC, EB | EIC/EB không duyệt được yêu cầu hủy | 🟡 |
| POST | `/api/v1/series/{id}/reject-cancellation` | EIC, EB | Series kẹt ở trạng thái chờ hủy | 🟡 |

### B — Vòng đời Submission

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|---|
| DELETE | `/api/v1/submissions/{id}` | Mangaka | Tạo nhầm Draft không xóa được | 🟢 |
| GET | `/api/v1/submissions/{id}/votes` | EB, EIC, Admin | Không xem được ai vote gì — thiếu minh bạch | 🟢 |

---

## MF2 — Chapter Production & Task Assignment `👤 Nam`

### B — Quản lý Series & Studio

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|---|
| PUT | `/api/v1/series/{id}` | Mangaka | Không sửa được metadata sau khi approved | 🟡 |
| POST | `/api/v1/series/{id}/set-hiatus` | Mangaka, Admin | Không có trạng thái tạm nghỉ | 🟢 |
| POST | `/api/v1/series/{id}/reactivate` | Mangaka, Admin | Không khôi phục từ Hiatus được | 🟢 |
| GET | `/api/v1/studios/{seriesId}/members` | Mangaka, TE | Không biết ai đang trong studio | 🟢 |
| DELETE | `/api/v1/studios/{seriesId}/members/{assistantId}` | Mangaka | Không khai trừ được assistant | 🟢 |
| POST | `/api/v1/studios/invitations/{id}/cancel` | Mangaka | Gửi nhầm lời mời không thu hồi được | 🟢 |

### B — Quản lý Chapter & Trang vẽ

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|---|
| PUT | `/api/v1/chapters/{id}` | Mangaka | Nhập sai tiêu đề/trang không sửa được | 🟢 |
| DELETE | `/api/v1/chapters/{id}` | Mangaka | Chapter rác tồn tại vĩnh viễn | 🟢 |
| GET | `/api/v1/chapters/{id}/pages` | Mangaka, TE | Không quản lý danh sách trang vẽ của chapter | 🟡 |
| PATCH | `/api/v1/chapters/{id}/pages/{pageNum}/reassign` | Mangaka | Không đổi assistant cho trang cụ thể | 🟢 |

### B — Task & Layer

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|---|
| GET | `/api/v1/tasks/{pageTaskId}` | Mangaka, Assistant | Không xem được chi tiết 1 task | 🟡 |
| PATCH | `/api/v1/tasks/{pageTaskId}/deadline` | Mangaka | Không cập nhật được deadline khi tiến độ thay đổi | 🟢 |

---

## MF3 — Quality Assurance & Publishing `👤 _(chưa assign)_`

### B — QA History & Pins

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|---|
| GET | `/api/v1/qa/chapters/{id}/history` | TE, Mangaka, Admin | Không truy vết lịch sử QA — bao nhiêu lần, sửa gì | 🟢 |
| POST | `/api/v1/qa/chapters/{id}/reopen` | TE | Không mở lại được QA nếu phát hiện sót lỗi | 🟢 |
| PATCH | `/api/v1/qa/pins/{pinId}` | TE | Không sửa được nội dung pin tạo nhầm | 🟢 |
| DELETE | `/api/v1/qa/pins/{pinId}` | TE | Không xóa được pin tạo nhầm | 🟢 |

### B — Publishing Schedule

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|---|
| GET | `/api/v1/publishing/chapters/{id}` | EB, Admin, TE | Không xem được chi tiết trạng thái phát hành | 🟢 |
| PATCH | `/api/v1/publishing/schedule/{id}` | EB | Lên lịch sai ngày không sửa được | 🟢 |
| DELETE | `/api/v1/publishing/schedule/{id}` | EB | Lên lịch nhầm phải nhờ Admin can thiệp DB | 🟢 |

---

## Core — Identity, Notifications & Infrastructure (Chia theo Owner)

### B — Identity & Tài Khoản (Người làm: **Nam** — Core - Phần 1)

| Method | Route | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/users/me` | FE không biết tên/avatar/role sau login — phải giải mã JWT thủ công | 🔴 |
| PUT | `/api/v1/users/me/change-password` | Không tự đổi được mật khẩu — phải nhờ Admin reset | 🟢 |
| POST | `/api/v1/auth/forgot-password` | Quên mật khẩu → không có cách vào lại tài khoản | 🟢 |
| POST | `/api/v1/auth/reset-password` | Đi kèm forgot-password | 🟢 |
| PUT | `/api/v1/users/me/avatar` | Mọi user dùng avatar mặc định, không cá nhân hóa | 🟢 |

> `POST /auth/logout` và `POST /auth/refresh` **đã có** — bỏ qua tài liệu cũ ghi là "thiếu".

### B — Notifications (Người làm: **Nam** — Core - Phần 1)

| Method | Route | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/notifications/unread-count` | Không có badge số chưa đọc trên Navbar | 🟡 |
| DELETE | `/api/v1/notifications/{id}` | Không dọn được thông báo đơn lẻ | 🟢 |
| DELETE | `/api/v1/notifications` | Không xóa hàng loạt thông báo đã đọc | 🟢 |

### B — Infrastructure & Services (Bắt buộc trước go-live)

| Service | Tình trạng hiện tại | Hậu quả nếu thiếu | Ưu tiên | Người làm BE |
|---|---|---|---|---|
| **SignalR Hub** | Logic có nhưng Hub chưa đăng ký endpoint | Notification chỉ thấy khi refresh trang, không real-time | ⚪ | **Nam** (Core - Phần 1) |
| **Scheduled Publisher Job** | Lưu DB nhưng không có job trigger | Chapter lên lịch không bao giờ tự phát hành | ⚪ | **Bach** (Core - Phần 2) |
| **File Storage (S3/Blob)** | FE tự nhập URL thủ công | Không upload được ảnh bìa, bản thảo, layer vẽ | ⚪ | **Bao** (Core - Phần 3) |
| **Email Service** | Chỉ log ra console | Email kích hoạt & reset password không đến được user | ⚪ | **Nam** (Core - Phần 1) |
| **Global Exception Handler** | Mỗi controller tự try-catch riêng | FE nhận HTML error page thay vì JSON → app crash phía client | ⚪ | **Bao** (Core - Phần 3) |
| **Audit Log** | Không có | Không điều tra được sự cố — ai làm gì, lúc mấy giờ | ⚪ | **Bao** (Core - Phần 3) |
| **Rate Limiter** | Không có | Dễ bị spam login/vote — lỗ hổng bảo mật | ⚪ | **Nam** (Core - Phần 1) |
| **Health Check** | Không có `GET /health` | Deployment/monitoring không biết service có đang sống | ⚪ | **Bach** (Core - Phần 2) |

---

# 📅 Lộ Trình Triển Khai

| Phase | Nội dung | Luồng | Ưu tiên |
|---|---|---|---|
| **Phase 0 — Quick Wins** | FE kết nối các trang đang gọi sai (Notifications, Board Voting, Editor Dashboard, Assistant pages, QA Canvas) — không cần code BE | MF1 + MF2 + MF3 + Core | 🔴 ROI cao nhất |
| **Phase 1 — Fix Bugs & Unblock Core** | Fix Bug #1 và #2 từ PR #19; viết `GET /users/me` (**Nam**); viết 2 queue TE (`/chapters/my-queue`, `/publishing/chapters/my-queue`); viết Admin Dashboard (**Bao**) | Core + MF2 + MF3 | 🔴 |
| **Phase 2 — Ranking Module** | Toàn bộ Infrastructure + Application + Controller Ranking (Phương án A thủ công) (**Bach**) | Core | 🟡 Khối lượng lớn nhất |
| **Phase 3 — Cancellation Flow** | request/approve/reject-cancellation đúng phân quyền EB/EIC; cancellation-queue; board/reports | MF1 | 🟡 |
| **Phase 4 — CRUD Hoàn Chỉnh** | PUT/DELETE series, chapter; Studio members; Reassign trang; Task deadline; QA pins; Publishing schedule | MF1 + MF2 + MF3 | 🟡–🟢 |
| **Phase 5 — Identity & Notifications** | change-password, forgot/reset-password, avatar; notifications unread-count, delete (**Nam**) | Core | 🟢 |
| **Phase 6 — History & Transparency** | submissions/{id}/votes; QA history/reopen; tasks/{id} detail; publishing/{id} detail | MF1 + MF2 + MF3 | 🟢 |
| **Phase 7 — Production Infrastructure** | SignalR Hub, Email SMTP, Rate Limiter (**Nam**) <br> Scheduled Publisher Job, Health Check (**Bach**) <br> File Storage thật, Global Exception Handler, Audit Log (**Bao**) | Infra | ⚪ Bắt buộc trước go-live |

---

## 🚫 Ngoài Phạm Vi (Không làm giai đoạn này)

- `/admin/ai`, `/admin/storage` — giữ `EmptyBackendState` hoặc ẩn menu
- `DiscoverPage`, `TrendingPage`, `GenresPage` — dùng mock data
- Reports nâng cao riêng — tái dùng số liệu từ Admin Dashboard + Ranking

---

## 📊 Tổng Kết Số Liệu

| Hạng mục | Số lượng |
|---|---|
| ✅ API BE đã có, FE chưa gọi đúng | 9 trang |
| 🔨 API cần viết mới (GAP A — để demo) | ~13 endpoints |
| 🔨 API cần viết mới (GAP B — production) | ~27 endpoints |
| 🔧 Service/Infrastructure thiếu | 8 items |
| 🆕 API mới hoàn thành (PR #19 nam1) | 3 endpoints |
| 🐛 Bugs cần fix từ code review | 2 nghiêm trọng, 1 minor UX, 1 performance |

> **Rule of thumb:** Phase 0–2 = đủ để demo. Phase 3–7 = đủ để deploy production thật.

---

## 📋 Checklist Bugs (Team Action Items)

| # | File | Mức độ | Người fix | Trạng thái |
|---|---|---|---|---|
| Bug #1 | `BulkReviewLayersHandler.cs` — thiếu `_pageTaskRepo.SaveChangesAsync` | 🔴 Nghiêm trọng | _(chưa assign)_ | ⬜ Chưa fix |
| Bug #2 | `GetLayerHistoryHandler.cs` — status suy luận từ `RejectionNote` sai | 🔴 Nghiêm trọng | _(chưa assign)_ | ⬜ Chưa fix |
| Issue #3 | `GetLayerHistoryHandler.cs` — `?status=Pending` trả `[]` không báo lỗi | 🟡 Minor UX | _(chưa assign)_ | ⬜ Chưa fix |
| Minor #4 | `BulkActivatePageTasksHandler.cs` — N+1 queries khi giao nhiều trang | 🟢 Performance | _(chưa assign)_ | ⬜ Có thể để sau |

