# 🗺️ MangaERP — Kế Hoạch Hoàn Thiện Hệ Thống

<<<<<<< HEAD
> **Nguồn:** Tổng hợp từ `MangaERP_Tong_Hop_Gap_Analysis.md` + `MangaERP_Tong_Hop_Gap_Analysis (1).md`
> **Cập nhật lần cuối:** 2026-06-30 | **Mục tiêu:** Phân rõ việc cần làm để **demo được** vs **deploy production được**
=======
> **Cập nhật lần cuối:** 2026-07-07  
> **Bố cục:** Chia theo người phụ trách — mỗi người có 2 mục: **Gap với FE** (BE đã có, FE chưa gọi / gọi sai) và **Gap với Production** (BE chưa làm hoặc chưa hoàn chỉnh).
>>>>>>> origin/bao

---

## 📌 Changelog — Tiến Độ Thực Tế

| Ngày | PR / Người | Nội dung | Ghi chú |
|---|---|---|---|
| 2026-06-30 | PR #18 `bach-v2` | `SharedInfrastructureExtensions`: hỗ trợ URI format cho PostgreSQL connection string khi deploy | Không ảnh hưởng local dev |
<<<<<<< HEAD
| 2026-06-30 | PR #19 `nam1` | **3 features mới** (xem chi tiết bên dưới) — merge vào main | ✅ Đã pull về local |
| 2026-07-01 | `bao` | **9 endpoints mới** Phase 1 + Phase 3 MF1 (xem chi tiết bên dưới) — build ✅ 0 errors | ⏳ Chưa push — đang review |

### ✅ Hoàn thành trong PR #19 `nam1`

| Feature | API | File | Trạng thái |
|---|---|---|---|
| Giao nhiều trang cho Assistant cùng lúc | `POST /api/v1/chapters/{id}/pages/bulk-activate` | `BulkActivatePageTasksHandler.cs` | ✅ Đã merge |
| Duyệt hàng loạt layer của Assistant | `POST /api/v1/tasks/bulk-review` | `BulkReviewLayersHandler.cs` | ✅ Đã merge |
| Xem lịch sử phiên bản layer | `GET /api/v1/tasks/{pageTaskId}/layers` | `GetLayerHistoryHandler.cs` | ✅ Đã merge |

> **Tác động lên plan:** Mục B3 — `GET /api/v1/tasks/{pageTaskId}/layers` đã hoàn thành.

### ✅ Hoàn thành ngày 2026-07-01 — Branch `bao`

| Feature | API | File chính | Trạng thái |
|---|---|---|---|
| Admin Dashboard (User + Submission + Series stats, incl. EiC) | `GET /api/v1/admin/dashboard` | `AdminDashboardController.cs` + `GetAdminDashboardHandler.cs` | ✅ Build OK |
| Board Reports (rolling 30 ngày) | `GET /api/v1/board/reports` | `BoardController.cs` + `GetBoardReportsHandler.cs` | ✅ Build OK |
| Xem tất cả series + filter status | `GET /api/v1/series?status=` | `GetAllSeriesHandler.cs` | ✅ Build OK |
| Cancellation queue cho EB/EiC | `GET /api/v1/series/cancellation-queue` | `GetCancellationQueueHandler.cs` | ✅ Build OK |
| Mangaka gửi yêu cầu hủy series | `POST /api/v1/series/{id}/request-cancellation` | `RequestCancellationHandler.cs` + Validator | ✅ Build OK |
| EB/EiC duyệt yêu cầu hủy | `POST /api/v1/series/{id}/approve-cancellation` | `ApproveCancellationHandler.cs` | ✅ Build OK |
| EB/EiC từ chối yêu cầu hủy | `POST /api/v1/series/{id}/reject-cancellation` | `RejectCancellationHandler.cs` + Validator | ✅ Build OK |
| Mangaka xóa Draft submission | `DELETE /api/v1/submissions/{id}` | `DeleteDraftHandler.cs` + Validator | ✅ Build OK |
| EB/EiC/Admin xem phiếu bầu | `GET /api/v1/submissions/{id}/votes` | `GetSubmissionVotesHandler.cs` | ✅ Build OK |

**Domain mới:** `MangaSeries` — thêm `CancellationRequestStatus` enum + 7 fields + 3 domain methods (`RequestCancellation`, `ApproveCancellation`, `RejectCancellation`)
**EF Migration:** `AddCancellationFieldsToSeries` — cần chạy `dotnet ef database update` trước khi test
**Audit Log:** `GET /api/v1/admin/dashboard` ghi `SystemAuditLog` vào DB (guardrail 1.2)
**Kiến trúc:** Cross-module stats queries (Dashboard/BoardReports) dùng `IMediator.Send()` thay vì inject repository trực tiếp (guardrail 0.3 + 7.5)

> **Tác động lên plan:** Phase 1 (Admin Dashboard) + Phase 3 (Cancellation Flow) + một phần Phase 6 (votes, delete draft) đã hoàn thành.

---

## 🐛 Bugs Cần Fix — Phát Hiện Từ Code Review PR #19

> ⚠️ **Assign cho đúng người trước khi làm phase tiếp theo.**

### Bug #1 — `BulkReviewLayersHandler.cs` thiếu `SaveChangesAsync` cho PageTask [NGHIÊM TRỌNG]

- **File:** `MangaERP.Task/Application/Commands/BulkReviewLayers/BulkReviewLayersHandler.cs`
- **Dòng:** ~113
- **Vấn đề:** Chỉ gọi `_layerRepo.SaveChangesAsync(ct)` nhưng **thiếu** `_pageTaskRepo.SaveChangesAsync(ct)`. Trạng thái `pageTask.Accept()` / `pageTask.RequestRevision()` có thể không được persist vào DB nếu hai repo dùng DbContext riêng.
- **Hậu quả:** Mangaka duyệt layer nhưng task status không thay đổi trong DB → FE hiển thị sai trạng thái.
- **Fix:** Thêm `await _pageTaskRepo.SaveChangesAsync(ct);` sau dòng `_layerRepo.SaveChangesAsync`.
- **Người fix:** Nam
- **Trạng thái:** 🔴 Chưa fix

### Bug #2 — `GetLayerHistoryHandler.cs` xác định Status sai logic [NGHIÊM TRỌNG]

- **File:** `MangaERP.Task/Application/Queries/GetLayerHistory/GetLayerHistoryHandler.cs`
- **Dòng:** ~103
- **Vấn đề:** `string statusStr = layer.RejectionNote == null ? "Accepted" : "Rejected"` — suy luận status từ `RejectionNote` thay vì dùng field/enum thật trên entity. Nếu layer có `RejectionNote` cũ nhưng sau đó được Accept → hiển thị sai là "Rejected".
- **Fix:** Dùng field `Status` hoặc enum có sẵn trên entity `ArtworkLayer` thay vì suy luận từ `RejectionNote`.
- **Người fix:** Nam
- **Trạng thái:** 🔴 Chưa fix

### Issue #3 — `GetLayerHistoryHandler.cs` filter `?status=Pending` trả về rỗng không rõ lý do [UX]

- **File:** `MangaERP.Task/Application/Queries/GetLayerHistory/GetLayerHistoryHandler.cs`
- **Dòng:** ~113–114
- **Vấn đề:** `ValidStatuses` bao gồm `"Pending"` nhưng handler lại skip toàn bộ Pending layers (vì `ReviewedAt == null`) → FE gọi `?status=Pending` luôn nhận `[]` mà không có thông báo lỗi.
- **Fix (2 lựa chọn):**
  - Bỏ `"Pending"` khỏi `ValidStatuses` trong Validator, trả `400 Bad Request` nếu FE gửi lên.
  - Hoặc: Hỗ trợ Pending thật sự bằng cách không filter `ReviewedAt != null` khi status là Pending.
- **Người fix:** Nam
- **Trạng thái:** 🟡 Minor — nhưng nên fix trước khi FE tích hợp

### Minor #4 — `BulkActivatePageTasksHandler.cs` N+1 queries [PERFORMANCE]

- **File:** `MangaERP.Chapter/Application/Commands/BulkActivatePageTasks/BulkActivatePageTasksHandler.cs`
- **Dòng:** ~59–65
- **Vấn đề:** Loop gọi `GetByChapterAndPageNumberAsync` từng trang một → 20 pages = 20 queries DB.
- **Fix:** Thêm method `GetByChapterAndPageNumbersAsync(chapterId, pageNumbers[])` vào `IPageTaskRepository` để lấy toàn bộ trong 1 query.
- **Người fix:** Nam
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
| `mangaErpService.ts` | Xóa `refreshToken` khỏi `login()`, thêm `refresh()` + `logout()` | Nam |
| `mangaErp.ts` | Xóa field `refreshToken` khỏi type `CurrentUser` | Nam |
| `httpClient.ts` | Thêm `credentials: "include"` | Nam |
| `authSession.ts` | Đảm bảo không clear gì liên quan cookie | Nam |
| `LoginPage.tsx` | Không lưu `refreshToken` vào localStorage | Nam |

---

# PHẦN A — GAP: Frontend ↔ Backend (Đủ để Demo)

> Mục tiêu: Xóa hết các trang đang hiển thị `EmptyBackendState`.

---

## MF1 — Series Proposal & Vetting `👤 Bao` (Duyệt Đề Xuất Truyện)

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)

| Trang FE | API đã có | Việc FE cần làm |
|---|---|---|
| Board Voting Center | `GET /api/v1/submissions/queue` | Hợp nhất route `/app/board/voting-center` → `SeriesProposalsPage.tsx` |
| Board Dashboard | `POST /api/v1/submissions/{id}/vote`<br>`POST /api/v1/submissions/{id}/resolve-conflict` | Wire API vote và resolve vào component |

### A — API thiếu cần viết mới

| Method | Route | Trang FE chờ | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| GET | `/api/v1/series/cancellation-queue` | `CancellationReviewPage.tsx` | 🟡 | ✅ Đã làm (2026-07-01) |
| GET | `/api/v1/board/reports` | `ReportsPage.tsx` (Board EB/EIC) | 🟡 | ✅ Đã làm (2026-07-01) |

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

## MF3 — Quality Assurance & Publishing `👤 Bach`

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)

| Trang FE | API đã có | Việc FE cần làm |
|---|---|---|
| `ReviewQueuePage.tsx` (Editor/QA) | `GET /api/v1/qa/chapters/{id}/pins`<br>`GET /api/v1/qa/chapters/{id}/session`<br>`POST /api/v1/qa/chapters/{id}/pins`<br>`POST /api/v1/qa/chapters/{id}/send-feedback` | Thêm QA calls vào service layer, dựng canvas ghim lỗi |

### A — API thiếu cần viết mới

| Method | Route | Trang FE chờ | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/publishing/chapters/my-queue` | `PublishingQueuePage.tsx` (Tantou Editor) | 🔴 |
| GET | `/api/v1/publishing/schedule` | `PublishingSchedulePage.tsx` | 🟡 |
| GET | `/api/v1/qa/queue` | `ReviewQueuePage.tsx` (QA Queue cho Editor nhận Chapter) | 🔴 |

---

## Core — Admin, Notifications & Ranking (Chia theo Owner)

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)
> **Người phụ trách:** **Nam** (Core - Phần 1) tích hợp SignalR; cả team wire API vào các dashboard tương ứng.

| Trang FE | API đã có | Việc FE cần làm |
|---|---|---|
| `AdminNotificationsPage`<br>`BoardNotificationsPage`<br>`editors/NotificationsPage` | `GET /api/v1/notifications`<br>`PATCH /api/v1/notifications/{id}/read`<br>`PATCH /api/v1/notifications/read-all` | Xóa `EmptyBackendState`, thêm call vào `mangaErpService.ts`, render thật; sau đó kết nối SignalR `/hubs/notifications` |

### A — API thiếu cần viết mới

| Method | Route | Trang FE chờ | Ưu tiên | Người làm BE | Trạng thái |
|---|---|---|---|---|---|
| GET | `/api/v1/admin/dashboard` | `AdminDashboardPage.tsx` | 🔴 | **Bao** (Core - Phần 3) | ✅ Đã làm (2026-07-01) |
| GET | `/api/v1/admin/workflow-stats` | `AdminWorkflowMonitoringPage.tsx` | 🟡 | **Bao** (Core - Phần 3) | ⬜ Chưa làm |
| GET | `/api/v1/admin/reports` | `AdminReportsAnalyticsPage.tsx` | 🟡 | **Bao** (Core - Phần 3) | ⬜ Chưa làm |
| GET | `/api/v1/admin/roles` | `AdminRolesPage.tsx` | 🟢 | **Bao** (Core - Phần 3) | ⬜ Chưa làm |

### A — Ranking Module (Khối lượng lớn nhất — toàn bộ cần viết mới)

> Hiện chỉ có Domain entities (`VoteData`, `RankingSnapshot`). Chưa có Application / Infrastructure / Controller.
> Trang FE chờ: `/ranking`, `RankingAnalyticsPage.tsx`, `RankingReportsPage.tsx`
> **Người phụ trách:** **Bach** (Core - Phần 2)

| Method | Route | Vai trò | Mô tả |
|---|---|---|---|
| POST | `/api/v1/ranking/import` | EB, EIC | Import phiếu bầu thô theo kỳ |
| POST | `/api/v1/ranking/compile` | EB, EIC | Gom vote, gán Rank, lưu Snapshot |
| GET | `/api/v1/ranking/board?period=...` | Public | Bảng xếp hạng chính thức |
| GET | `/api/v1/ranking/periods` | Public | Danh sách kỳ đã có snapshot |
| GET | `/api/v1/ranking/import/{period}` | EB, EIC | Xem phiếu thô đã import |
| DELETE | `/api/v1/ranking/import/{period}` | EB, EIC | Xóa phiếu thô trước khi compile |

**Checklist BE Ranking (Phương án A — thủ công):**
- [ ] Infrastructure: `IVoteDataRepository`, `IRankingSnapshotRepository`, EF mapping, migration (**Bach**)
- [ ] Application: `ImportVoteDataCommand`, `CompileRankingCommand`, `GetRankingBoardQuery` (**Bach**)
- [ ] Presentation: `RankingController.cs` (**Bach**)

---

# PHẦN B — GAP: System Completeness (Production-Ready)

> Mục tiêu: Người dùng thật không bị kẹt, không mất dữ liệu, không lỗi bất ngờ.

---

## MF1 — Series Proposal & Vetting `👤 Bao`

### B — Luồng Cancellation (Thiếu hoàn toàn — có method `Cancel()` nhưng sai phân quyền)

> ⚠️ Quyết định hủy truyện thuộc **EB/EIC**, không phải Admin.
> 
> 🛡️ **Nguyên tắc phân quyền hệ thống:** Vai trò **Admin** chỉ có quyền hạn kỹ thuật hệ thống (quản lý tài khoản, phân quyền, cấu hình hệ thống). Mọi nghiệp vụ vận hành, nội dung và xuất bản (bao gồm Duyệt hủy truyện, Lên lịch xuất bản, Biên tập và Ranking) thuộc quyền kiểm soát của Ban biên tập (**EB/EIC** hoặc **Editor**) và hoàn toàn không hiển thị/không cho phép Admin truy cập.

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|---|
| POST | `/api/v1/series/{id}/request-cancellation` | Mangaka | Luồng hủy đứt ngay bước đầu — Mangaka không gửi được | 🟡 | ✅ Đã làm (2026-07-01) |
| POST | `/api/v1/series/{id}/approve-cancellation` | EIC, EB | EIC/EB không duyệt được yêu cầu hủy | 🟡 | ✅ Đã làm (2026-07-01) |
| POST | `/api/v1/series/{id}/reject-cancellation` | EIC, EB | Series kẹt ở trạng thái chờ hủy | 🟡 | ✅ Đã làm (2026-07-01) |

### B — Vòng đời Submission

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|---|
| DELETE | `/api/v1/submissions/{id}` | Mangaka | Tạo nhầm Draft không xóa được | 🟢 | ✅ Đã làm (2026-07-01) |
| GET | `/api/v1/submissions/{id}/votes` | EB, EIC, Admin | Không xem được ai vote gì — thiếu minh bạch | 🟢 | ✅ Đã làm (2026-07-01) |

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

## MF3 — Quality Assurance & Publishing `👤 Bach`

### B — QA History & Pins

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|---|
| GET | `/api/v1/qa/chapters/{id}/history` | TE, Mangaka, Admin | Không truy vết lịch sử QA — bao nhiêu lần, sửa gì | 🟢 |
| POST | `/api/v1/qa/chapters/{id}/reopen` | TE | Không mở lại được QA nếu phát hiện sót lỗi | 🟢 |
| PATCH | `/api/v1/qa/pins/{pinId}` | TE | Không sửa được nội dung pin tạo nhầm | 🟢 |
| DELETE | `/api/v1/qa/pins/{pinId}` | TE | Không xóa được pin tạo nhầm | 🟢 |
| POST | `/api/v1/qa/pins/{pinId}/fixed` | Mangaka/Assistant | Không có nút báo cáo đã sửa lỗi (Bug pin status lifecycle) | 🟡 |
| POST | `/api/v1/qa/pins/{pinId}/assign-fix` | Mangaka | Không có API phân công Fix Task cho Assistant | 🟡 |

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

## 📌 Cập nhật — Media Upload (nối tiếp dòng 435)

**Trạng thái:** ✅ Upload private hoạt động, có magic bytes + reject extension nguy hiểm + chặn path traversal. Test coverage đủ.
**Còn thiếu:** Cơ chế di chuyển file từ private/ sang public/ khi chapter/cover được duyệt/publish — hiện chưa có endpoint hay logic nào gọi việc này. Cần bổ sung khi tích hợp vào luồng Approve/Publish thật (có thể là 1 method nội bộ MoveToPublicAsync(fileKey) gọi từ Handler duyệt chapter, không expose qua API public).
**Nợ kỹ thuật giữ nguyên:** Ownership check ở ViewPrivateFile chỉ dựa vào [Authorize] + GUID khó đoán, chưa có bảng metadata fileKey→uploaderId.

---

# 📅 Lộ Trình Triển Khai

| Phase | Nội dung | Luồng | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| **Phase 0 — Quick Wins** | FE kết nối các trang đang gọi sai (Notifications, Board Voting, Editor Dashboard, Assistant pages, QA Canvas) — không cần code BE | MF1 + MF2 + MF3 + Core | 🔴 ROI cao nhất | ⬜ FE việc |
| **Phase 1 — Fix Bugs & Unblock Core** | Fix Bug #1 và #2 từ PR #19; viết `GET /users/me` (**Nam**); viết 2 queue TE (`/chapters/my-queue`, `/publishing/chapters/my-queue`); viết Admin Dashboard (**Bao**) | Core + MF2 + MF3 | 🔴 | 🔄 Một phần: Admin Dashboard ✅ done Bao. Bugs #1 #2 + queue TE ⬜ chưa |
| **Phase 2 — Ranking Module** | Toàn bộ Infrastructure + Application + Controller Ranking (Phương án A thủ công) (**Bach**) | Core | 🟡 Khối lượng lớn nhất | ⬜ Chưa bắt đầu |
| **Phase 3 — Cancellation Flow** | request/approve/reject-cancellation đúng phân quyền EB/EIC; cancellation-queue; board/reports | MF1 | 🟡 | ✅ Hoàn thành (2026-07-01, Bao) |
| **Phase 4 — CRUD Hoàn Chỉnh** | PUT/DELETE series, chapter; Studio members; Reassign trang; Task deadline; QA pins; Publishing schedule | MF1 + MF2 + MF3 | 🟡–🟢 | ⬜ Chưa bắt đầu |
| **Phase 5 — Identity & Notifications** | change-password, forgot/reset-password, avatar; notifications unread-count, delete (**Nam**) | Core | 🟢 | ⬜ Chưa bắt đầu |
| **Phase 6 — History & Transparency** | submissions/{id}/votes; QA history/reopen; tasks/{id} detail; publishing/{id} detail | MF1 + MF2 + MF3 | 🟢 | 🔄 Một phần: votes + delete draft ✅ done Bao. Phần còn lại ⬜ |
| **Phase 7 — Production Infrastructure** | SignalR Hub, Email SMTP, Rate Limiter (**Nam**) <br> Scheduled Publisher Job, Health Check (**Bach**) <br> File Storage thật, Global Exception Handler, Audit Log (**Bao**) | Infra | ⚪ Bắt buộc trước go-live | 🔄 Một phần: Audit Log cho Admin Dashboard ✅. Còn lại ⬜ |
=======
| 2026-06-30 | PR #19 `nam1` | **3 features mới** (giao trang bulk, duyệt bulk, layer history) — merge vào main | ✅ Đã merge |
| 2026-07-01 | PR #25 `bao` | **9 endpoints mới** (Admin Dashboard, Board Reports, Cancellation Flow, Delete Draft, Votes history) | ✅ Đã merge |
| 2026-07-02 | PR #26 `nam1` | **Đổi assistant, thành viên studio, deadline, xem layer của Chapter** | ✅ Đã merge |
| 2026-07-02 | PR #27 `bach-v2` | **QA Queue, phân công Fix Task, báo lỗi sửa xong, Hủy lịch xuất bản (MF3)** | ✅ Đã merge |
| 2026-07-07 | `bao` | **Rà soát toàn hệ thống** + fix phân quyền + bổ sung 12 endpoints mới (Profile, Password, OTP, Notification, Board Reports, Infrastructure) | ✅ Đã xong |

---

## 👤 Phân Chia Ownership — Quy Tắc Bắt Buộc

> ⚠️ **Mỗi thành viên chỉ được commit vào các file thuộc mainflow mình phụ trách.**  
> Nếu cần sửa file của người khác, phải báo qua team chat và đợi confirm trước.

| Mainflow | Người phụ trách | Module / Thư mục |
|---|---|---|
| **MF1** — Series Proposal & Vetting | **Bao** `bao` | `MangaERP.Submission`, `MangaERP.Series`, `MangaERP.Studio` |
| **MF2** — Chapter Production & Task | **Nam** `nam1` | `MangaERP.Chapter`, `MangaERP.Task` |
| **MF3** — QA & Publishing | **Bach** `bach-v2` | `MangaERP.QA`, `MangaERP.Publishing` |
| **Core 1** — Identity, Security & Notifications | **Bao** `bao` | `MangaERP.Identity` (Auth/Profile, Notifications, SignalR, Email) |
| **Core 2** — Ranking & Background Jobs | **Bach** `bach-v2` | `MangaERP.Ranking`, Scheduled Publisher Job |
| **Core 3** — Admin Portal & Global Infrastructure | **Bao** `bao` | Admin endpoints, Middlewares, Health Checks, Rate Limiter |

**Quy tắc file cụ thể:**
- Không ai sửa `SharedInfrastructureExtensions.cs` hoặc `AppDbContext.cs` mà không báo cả nhóm.
- Migration EF Core: ai thêm entity thì người đó tạo migration.
- Controller module nào thì chỉ người phụ trách module đó được sửa.

**Ranh giới ownership Studio:**
- **Bao** phụ trách nền tảng Studio membership/invitation trong `MangaERP.Studio`: mời Assistant, tạo tài khoản Assistant qua invitation, danh sách lời mời, danh sách member, cancel invitation, accept/decline invitation, remove member ở mức membership.
- **Nam** phụ trách các phần Studio chạm vào production task trong `MangaERP.Chapter` / `MangaERP.Task`: task board, assign/reassign task, deadline, comments, layer version/rollback, và revoke/deassign `PageTask` khi Assistant bị remove khỏi studio.
- Riêng `DELETE /api/v1/studios/{seriesId}/members/{assistantId}`: Bao own endpoint/membership, Nam own task-side revocation. Endpoint phải gọi task-side service trước khi commit để tránh trạng thái nửa vời.

---

## 🛡️ Nguyên Tắc Phân Quyền Vai Trò

| Vai trò | Phạm vi | Giới hạn |
|---|---|---|
| **Admin** | Quản lý hạ tầng kỹ thuật: tài khoản, SAM config, system stats | **Không được** can thiệp bỏ phiếu, duyệt/hủy truyện, xuất bản, xếp hạng |
| **EditorialBoard** | Bỏ phiếu duyệt bản thảo, duyệt hủy truyện, lên lịch xuất bản | — |
| **EditorInChief** | Phân xử xung đột bỏ phiếu, toàn quyền EiC | — |

---

## 👤 BAO — MF1 + Core 1 + Core 3

### 📡 Gap với FE (BE đã có, FE chưa tích hợp đúng)

| Trang FE | API BE đã có | Việc FE cần làm | Ưu tiên |
|---|---|---|---|
| **Board Dashboard** | `GET /api/v1/board/reports`<br>`GET /api/v1/board/performance-reports`<br>`GET /api/v1/submissions/queue`<br>`POST /api/v1/submissions/{id}/vote`<br>`POST /api/v1/submissions/{id}/resolve-conflict` | Thay `EmptyBackendState` bằng gọi API thật, vẽ biểu đồ stats + reports EB. Không dùng `/admin/dashboard` để tránh sai lệch phân quyền. | 🔴 |
| **Voting Center** | `GET /api/v1/submissions/queue`<br>`GET /api/v1/submissions/{id}/votes`<br>`POST /api/v1/submissions/{id}/vote`<br>`POST /api/v1/submissions/{id}/resolve-conflict` | Dựng UI bỏ phiếu (Approve/Reject/Revision), xem lịch sử phiếu của từng thành viên EB và giải quyết xung đột của EiC | 🔴 |
| **Submission Detail / Revisions** | `GET /api/v1/submissions/{id}/feedback-pins`<br>`GET /api/v1/submissions/{id}/feedback-pins/history` | Dựng UI hiển thị chi tiết đề xuất bản thảo kèm các Ghim lỗi/Góp ý của EB và lịch sử qua các vòng | 🟡 |
| **Notifications Page** | `GET /api/v1/notifications`<br>`PATCH /api/v1/notifications/{id}/read`<br>`PATCH /api/v1/notifications/read-all` | Dựng UI thông báo thật, kết nối SignalR tới `/hubs/notifications` | 🔴 |
| **Profile & Auth Settings** | `GET /api/v1/users/me`<br>`PUT /api/v1/users/profile`<br>`PUT /api/v1/users/me/change-password`<br>`PUT /api/v1/users/me/avatar` | Tích hợp vào màn hình Cài đặt Profile cá nhân (PenName, Softwares, BankAccount) và Đổi mật khẩu | 🟢 |
| **Notification Badge** | `GET /api/v1/notifications/unread-count`<br>`DELETE /api/v1/notifications/{id}`<br>`DELETE /api/v1/notifications` | Đọc số lượng unread count cho Navbar badge và hỗ trợ nút xóa thông báo | 🟢 |
| **User & Role Monitoring** | `GET /api/v1/admin/roles`<br>`GET /api/v1/admin/workflow-stats` | Sử dụng cho trang giám sát phân quyền và thống kê vận hành hệ thống của Admin | 🟢 |

> ⚠️ **Contract Fix bắt buộc (FE cần sửa):**
> - FE đang gọi `POST /notifications/{id}/read` → Đúng phải là **PATCH**
> - FE đang đọc `refreshToken` từ response body `/login` → BE không còn trả về (dùng httpOnly Cookie)
> - FE chưa thêm `{ credentials: "include" }` vào fetch → Token cookie không được gửi kèm request

### 🏭 Gap với Production (BE chưa làm / cần hoàn thiện)

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| `GET /api/v1/users/me` | ✅ **Đã xong** | Profile user sau login |
| `PUT /api/v1/users/me/change-password` | ✅ **Đã xong** | Tự đổi mật khẩu chủ động |
| `PUT /api/v1/users/me/avatar` | ✅ **Đã xong** | Cập nhật ảnh đại diện |
| `POST /api/v1/auth/forgot-password` | ✅ **Đã xong** | OTP gửi về `PersonalEmail` thật |
| `POST /api/v1/auth/reset-password` | ✅ **Đã xong** | Đặt lại mật khẩu qua OTP |
| `GET /api/v1/notifications/unread-count` | ✅ **Đã xong** | Badge đếm thông báo chưa đọc |
| `DELETE /api/v1/notifications/{id}` | ✅ **Đã xong** | Xóa thông báo đơn lẻ |
| `DELETE /api/v1/notifications` | ✅ **Đã xong** | Xóa hàng loạt thông báo đã đọc |
| `GET /api/v1/admin/workflow-stats` | ✅ **Đã xong** | Thống kê chapter/submission/task theo trạng thái |
| `GET /api/v1/admin/roles` | ✅ **Đã xong** | Static roles reference cho FE dropdown |
| `GET /api/v1/board/reports` | ✅ **Đã xong** | Summary submissions/cancellations cho Board Dashboard |
| `GET /api/v1/board/performance-reports` | ✅ **Đã xong (2026-07-07)** | Báo cáo hiệu suất EB/EiC (approve/reject rate, thời gian xử lý, trend) |
| Global Exception Middleware | ✅ **Đã xong** | Chuẩn hóa JSON error response toàn hệ thống |
| Health Check (`/health`, `/health/live`) | ✅ **Đã xong** | DB + Cache probe |
| Rate Limiter (`GlobalLimiter` + `AuthPolicy`) | ✅ **Đã xong** | Chống brute-force login/OTP |
| Token Blacklist (Logout security) | ✅ **Đã xong** | JTI-based blacklist qua IMemoryCache |
| Authorization fixes (MF1) | ✅ **Đã xong** | Gỡ `TantouEditor` khỏi submission flow, thêm `EditorInChief` vào Series |
| **Privacy Gap: View Series** | ✅ **Đã fix (2026-07-07)** | `GET /api/v1/series` và `GET /api/v1/series/{id}` đã giới hạn `TantouEditor` chỉ xem các series của Mangaka có `ManagingTantouId` trỏ về chính editor đó |
| **Brevo SMTP API Key** | ❌ **Chưa cấu hình** | Cấu hình biến môi trường `Brevo:ApiKey` (hoặc `Brevo__ApiKey`) trên Production để gửi email thật (hiện đang log ra console nếu không có key) |
| **Duplicate Route: Board Reports** | ✅ **Đã fix (2026-07-07)** | Giải quyết xung đột Ambiguous route bằng cách đổi endpoint trong `BoardReportsController` sang `performance-reports` |
| **Auto-Resolution Job** | ⬜ **Backlog** | Background job tự giải quyết Submission hết hạn vote theo đa số phiếu |
| **Submission Revisions** | ⬜ **Backlog** | `POST /submissions/{id}/revisions` — Mangaka nộp lại bản sửa, giữ lịch sử vote |

---

## 👤 NAM — MF2 (Chapter Production & Task)

### 📡 Gap với FE (BE đã có, FE chưa tích hợp đúng)

| Trang FE | API BE đã có | Việc FE cần làm | Ưu tiên |
|---|---|---|---|
| **Editor Dashboard (Tantou)** | `GET /api/v1/series`<br>`GET /api/v1/chapters/series/{seriesId}` | Thay mock data bằng gọi API với JWT token. Đúng path `/chapters/series/{seriesId}` | 🔴 |
| **Assistant Pages** | `GET /api/v1/tasks/assigned`<br>`POST /api/v1/tasks/{pageTaskId}/layers` | Kết nối màn hình nhận task và upload artwork layer. Đúng path `/tasks/{pageTaskId}/layers` | 🔴 |

### 🏭 Gap với Production (BE chưa làm / cần hoàn thiện)

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| `GET /api/v1/chapters/my-queue` | ✅ **Đã xong** | Hàng đợi chapter Tantou Editor cần duyệt — **Blocker** |
| **Logic Bug: `CreateChapter`** | ✅ **Đã fix** | Phải tự động gán `AssignedEditorId` từ `ManagingTantouId` của Mangaka, không để Mangaka chọn thủ công |
| **Authorization Fix: `TasksController`** | ✅ **Đã fix** | `GET /tasks/layers/history` thiếu role `TantouEditor` — TE không xem được lịch sử layer khi review |
| **Authorization Fix: `ChaptersController`** | ✅ **Đã fix** | `GET /chapters/series/{seriesId}` thiếu role `EditorInChief` |
| `GET /api/v1/assistant/tasks/income` | ✅ **Đã xong** | Thống kê hoàn thành task + thu nhập của Assistant |
| `PUT /api/v1/series/{id}` | ✅ **Đã xong** | Sửa metadata series sau khi đã được approved |
| `POST /api/v1/series/{id}/set-hiatus` | ✅ **Đã xong** | Chuyển series sang trạng thái tạm ngưng |
| `POST /api/v1/series/{id}/reactivate` | ✅ **Đã xong** | Kích hoạt lại series từ Hiatus |
| `DELETE /api/v1/studios/{seriesId}/members/{assistantId}` | ✅ **Đã xong** | Đã hoàn thiện logic revoke/deassign `PageTask` thật đồng bộ trong transaction |
| `PUT/DELETE /api/v1/chapters/{id}` | ✅ **Đã xong** | Sửa hoặc xóa chapter (đã có từ trước) |
| `GET /api/v1/studios/{seriesId}/tasks/board` | ✅ **Đã fix** | Kanban board đã có, đã check requester có quyền với series/studio |
| `GET /api/v1/tasks/{pageTaskId}/layers/{layerType}/versions` | ✅ **Đã fix** | API đã có, đã check quyền theo task/series trước khi trả layer URLs/history |
| `POST /api/v1/tasks/{pageTaskId}/layers/{layerType}/rollback` | ✅ **Đã fix** | API đã có, rollback chỉ cho Mangaka owner của series |
| **Task Comments** | ✅ **Đã fix** | `GET/POST /tasks/{id}/comments` đã có, đã check user có quyền trên task |
| **Assistant Recommendation** | ✅ **Đã fix** | `GET /chapters/{id}/recommend-assistants` đã có, đã check chapter thuộc Mangaka đang gọi |

### 🔧 Logic / Authorization Nam cần fix

| Hạng mục | Mức độ | Trạng thái | Yêu cầu sửa |
|---|---|---|---|
| `PUT/PATCH /api/v1/chapters/{id}` | 🔴 | ✅ **Đã fix** | Không cho Mangaka truyền `AssignedEditorId` từ request để tự đổi Tantou Editor. Bỏ field này khỏi request/command hoặc luôn giữ/recompute từ `Mangaka.ManagingTantouId`. |
| `GET /api/v1/studios/{seriesId}/tasks/board` | 🔴 | ✅ **Đã fix** | Query nhận requester context. Mangaka chỉ xem series của mình; Tantou chỉ xem series thuộc Mangaka mình quản lý; Assistant chỉ xem studio mà mình là accepted member. |
| `GET /api/v1/chapters/{id}/recommend-assistants` | 🟡 | ✅ **Đã fix** | Load chapter -> series và check `series.AuthorId == RequesterId` trước khi trả danh sách assistant/workload. |
| `GET/POST /api/v1/tasks/{id}/comments` | 🔴 | ✅ **Đã fix** | Cả read và add comment phải check quyền theo task: Mangaka owner, Assistant assigned/accepted member theo rule, Tantou quản lý series/chapter. User không liên quan trả 403. |
| Layer versions / rollback | 🔴 | ✅ **Đã fix** | `GET /tasks/{pageTaskId}/layers/{layerType}/versions` và `POST /tasks/{pageTaskId}/layers/{layerType}/rollback` phải check quyền theo task/series. Rollback chỉ cho Mangaka owner. |
| Remove Assistant task revocation | 🔴 | ✅ **Đã fix** | Thay `NoOpStudioTaskRevocationService` bằng implementation thật: tìm `PageTask` assigned cho `assistantId`, thuộc `seriesId`, bỏ qua `Approved`, gọi `task.Revoke()`, không `SaveChangesAsync` riêng để commit chung với Studio membership. |

---

## 👤 BACH — MF3 + Core 2 (QA, Publishing & Ranking)

### 📡 Gap với FE (BE đã có, FE chưa tích hợp đúng)

| Trang FE | API BE đã có | Việc FE cần làm | Ưu tiên |
|---|---|---|---|
| **Review Queue Page (QA Canvas)** | `GET /api/v1/qa/chapters/{id}/pins`<br>`GET /api/v1/qa/chapters/{id}/session`<br>`POST /api/v1/qa/chapters/{id}/pins`<br>`POST /api/v1/qa/chapters/{id}/send-feedback` | Tích hợp QA calls vào service, dựng canvas ghim lỗi lên ảnh | 🔴 |
| **QA Queue Màn Hình** | `GET /api/v1/qa/queue` | Dựng trang danh sách chapter chờ review QA (ReadyForQA) thuộc quyền quản lý của Tantou Editor | 🔴 |
| **QA Bug Fixing (Xử lý lỗi)** | `POST /api/v1/qa/pins/{pinId}/assign-fix`<br>`POST /api/v1/qa/pins/{pinId}/fixed` | Màn hình giao lỗi của Mangaka cho Assistant sửa và nút báo lỗi đã sửa xong của Trợ lý | 🟡 |

> ⚠️ **Contract Fix bắt buộc (FE cần sửa — QA routes bị outdated):**
> - Sai: `/api/v1/qa/{sessionId}/pins` và `/api/v1/qa/{sessionId}/feedback-batch`
> - Đúng: route gắn với **Chapter ID**: `/api/v1/qa/chapters/{chapterId}/...`

### 🏭 Gap với Production (BE chưa làm / cần hoàn thiện)

| Hạng mục | Trạng thái | Ghi chú |
|---|---|---|
| `GET /api/v1/publishing/chapters/my-queue` | ❌ **Chưa làm** | Hàng đợi phát hành chapter của Tantou Editor — **Blocker** |
| `GET /api/v1/publishing/schedule` | ❌ **Chưa làm** | Lấy danh sách/lịch phát hành (calendar view) |
| `GET /api/v1/publishing/chapters/ready` | ❌ **Chưa làm** | Queue chapter đã qua QA (`Status == Approved`), chờ EB lên lịch — **Blocker** |
| `GET /api/v1/publishing/chapters/{id}` | ❌ **Chưa làm** | Xem chi tiết trạng thái phát hành chapter |
| `GET /api/v1/qa/chapters/{id}/history` | ❌ **Chưa làm** | Lịch sử timeline QA (ghim lỗi, sửa, duyệt) |
| `POST /api/v1/qa/chapters/{id}/reopen` | ❌ **Chưa làm** | Mở lại phiên QA khi phát hiện sót lỗi |
| `PATCH/DELETE /api/v1/qa/pins/{pinId}` | ❌ **Chưa làm** | Sửa hoặc xóa ghim lỗi QA |
| **Authorization Fix: QA Handlers** | ❌ **Chưa fix** | AddBugPin, SendFeedback, ApproveChapter chưa check chapter.AssignedEditorId == request.EditorId |
| **Contract Check: SchedulePublish** | ❌ **Chưa fix** | SchedulePublishCommand nhận SeriesId nhưng handler không kiểm chéo với chapter.SeriesId |
| **Missing side-effect: ChapterApprovedHandler** | ❌ **Chưa fix** | `ChapterApprovedHandler` chỉ gửi thông báo cho Mangaka, thiếu gửi thông báo cho EditorialBoard/EditorInChief khi chapter được duyệt |
| **Ranking Module** | ❌ **Chưa làm** | Toàn bộ module Ranking chưa được đăng ký và chưa có Controller/Service |
| **Scheduled Publisher Job** | ❌ **Chưa làm** | Background job tự động publish chapter đúng giờ theo lịch đã đặt |
| **QA Pin Severity & Categories** | ⬜ **Backlog** | Thêm `Severity` (Blocker/Major/Minor) và `Category` vào `QaPin` |
| **Publishing Conflict Checker** | ⬜ **Backlog** | Kiểm tra xung đột lịch phát hành để tránh đảo lộn thứ tự chapter |
>>>>>>> origin/bao

---

## 🚫 Ngoài Phạm Vi (Không làm giai đoạn này)

<<<<<<< HEAD
- `/admin/ai`, `/admin/storage` — giữ `EmptyBackendState` hoặc ẩn menu
- `DiscoverPage`, `TrendingPage`, `GenresPage` — dùng mock data
- Reports nâng cao riêng — tái dùng số liệu từ Admin Dashboard + Ranking

---

## 📊 Tổng Kết Số Liệu

| Hạng mục | Số lượng |
|---|---|
| ✅ API BE đã có, FE chưa gọi đúng | 9 trang |
| 🔨 API cần viết mới (GAP A — để demo) | ~13 endpoints (còn ~11 sau khi Bao hoàn thành) |
| 🔨 API cần viết mới (GAP B — production) | ~27 endpoints (còn ~22 sau khi Bao hoàn thành) |
| 🔧 Service/Infrastructure thiếu | 8 items (Audit Log partially done) |
| 🆕 API mới hoàn thành (PR #19 nam1) | 3 endpoints |
| 🆕 API mới hoàn thành (2026-07-01 Bao) | **9 endpoints** (Phase 1 Admin Dashboard + Phase 3 Cancellation + Phase 6 partial) |
| 🐛 Bugs cần fix từ code review | 2 nghiêm trọng, 1 minor UX, 1 performance |
| ⚠️ Tech Debt (Bao, note lại) | Pagination cho `GET /api/v1/series` + `GET /api/v1/admin/dashboard` dùng COUNT aggregate thay GetAll |

> **Rule of thumb:** Phase 0–2 = đủ để demo. Phase 3–7 = đủ để deploy production thật.

---

## 📋 Checklist Bugs (Team Action Items)

| # | File | Mức độ | Người fix | Trạng thái |
|---|---|---|---|---|
| Bug #1 | `BulkReviewLayersHandler.cs` — thiếu `_pageTaskRepo.SaveChangesAsync` | 🔴 Nghiêm trọng | Nam | ⬜ Chưa fix |
| Bug #2 | `GetLayerHistoryHandler.cs` — status suy luận từ `RejectionNote` sai | 🔴 Nghiêm trọng | Nam | ⬜ Chưa fix |
| Issue #3 | `GetLayerHistoryHandler.cs` — `?status=Pending` trả `[]` không báo lỗi | 🟡 Minor UX | Nam | ⬜ Chưa fix |
| Minor #4 | `BulkActivatePageTasksHandler.cs` — N+1 queries khi giao nhiều trang | 🟢 Performance | Nam | ⬜ Có thể để sau |

---

## 📌 Lộ Trình Hoàn Thiện Module Segmentation (SAM) — Phase 3-6

### Phase 3 — Invert Mask (Quyết định kỹ thuật: Làm ở FE)
- **FE:** Decode RLE → Invert trên canvas phụ (bằng XOR composite hoặc pixel manipulation) → Re-encode RLE và gửi lên API khi giao việc.
- **BE:** Không cần thay đổi gì.

### Phase 4 — Khóa Vùng Vẽ Cho Assistant (FE + BE `👤 Bao`)
- **FE:** Gọi `GET /api/segmentation/tasks/mine?status=Pending` → Decode RLE → Dựng clip mask chặn vẽ ngoài vùng.
- **BE:** Thêm thông tin kích thước ảnh gốc `ImageSize` (OriginalWidth/OriginalHeight) vào `SegmentationTaskDto` để FE scale đúng mask khi canvas zoom/pan.

### Phase 5 — Content Moderation Pipeline (BE `👤 Bao`)
- **BE (S3/File Storage):** Sau khi ảnh được upload, gọi `SamServiceClient` chạy `/embedding` để quét xem ảnh có hợp lệ/không bị hỏng trước khi cho phép publish.
- *Giới hạn:* Chỉ kiểm tra tính hợp lệ cơ bản của ảnh, không phải bộ lọc NSFW đầy đủ. Nếu Colab sập, chỉ ghi log cảnh báo và bỏ qua (không chặn luồng chính).

### Phase 6 — Test & Rollout (BE `👤 Bao`)
- Test tích hợp Circuit Breaker khi Colab ngắt kết nối.
- Chạy 3 lệnh grep kiểm tra cách ly module (Segmentation cô lập hoàn toàn khỏi Task, Chapter, Identity).
- Chạy `dotnet ef database update` cho Segmentation DbContext để đồng bộ bảng vật lý lên DB dev thật.

### 🔗 Tích Hợp Hệ Thống Event (BE `👤 Nam` — MF2 & Core 1)
- **Tình trạng:** Khi một Segmentation Task được tạo, module `Segmentation` sẽ phát đi sự kiện `SegmentationTaskAssignedEvent` qua MediatR.
- **Nhiệm vụ của Nam:** Viết một Handler tiêu thụ sự kiện này trong module `Task`/`Notification` để tự động tạo bản ghi `Notification` và gửi thông báo Real-time (qua SignalR Hub `/hubs/notifications`) cho Assistant được giao việc.

=======
- `/admin/reports` — Admin không liên quan quy trình nghiệp vụ nội dung, bỏ qua
- `/admin/ai`, `/admin/storage` — giữ `EmptyBackendState` hoặc ẩn menu
- `DiscoverPage`, `TrendingPage`, `GenresPage` — dùng mock data
- Dynamic Image Optimization (Cloudinary `q_auto,f_auto`) — Backlog thấp

---

## 📋 Bugs Đã Giải Quyết

| # | File | Mức độ | Người fix | Trạng thái |
|---|---|---|---|---|
| Bug #1 | `BulkReviewLayersHandler.cs` — thiếu `SaveChangesAsync` | 🔴 Nghiêm trọng | Nam | ✅ Đã fix |
| Bug #2 | `GetLayerHistoryHandler.cs` — status suy luận từ `RejectionNote` sai | 🔴 Nghiêm trọng | Nam | ✅ Đã fix |
| Issue #3 | `GetLayerHistoryHandler.cs` — `?status=Pending` trả `[]` không báo lỗi | 🟡 Minor UX | Nam | ✅ Đã fix |
| Minor #4 | `BulkActivatePageTasksHandler.cs` — N+1 queries | 🟢 Performance | Nam | ✅ Đã fix |
| Bug #5 | `SubmissionsController` — `TantouEditor` sai phạm vi phân quyền | 🟡 Authorization | Bao | ✅ Đã fix |
| Bug #6 | `PublishingController` — `Admin` trong role list `POST /publish` | 🟡 Authorization | Bao | ✅ Đã fix |
| Bug #7 | `SeriesController` — `GET /series/{id}` thiếu `EditorInChief` | 🟢 Authorization | Bao | ✅ Đã fix |

---

## 📌 Lộ Trình Module Segmentation (SAM) — Phase còn lại

| Phase | Mô tả | Trạng thái | Người |
|---|---|---|---|
| Phase 3 — Invert Mask | FE: Decode RLE → Invert canvas → Re-encode | ⬜ FE việc | FE |
| Phase 4 — Khóa Vùng Vẽ | BE ✅ Xong. FE: Gọi `GET /segmentation/tasks/mine` → Dựng clip mask | ✅ BE xong | FE còn |
| Phase 5 — Content Moderation | Sau upload ảnh → gọi SAM `/embedding` validate | ⬜ Backlog | Bao |
| Phase 6 — Test & Rollout | Circuit Breaker test + `dotnet ef database update` Segmentation | ⬜ Backlog | Bao |
>>>>>>> origin/bao
