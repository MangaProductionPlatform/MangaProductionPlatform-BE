# 🗺️ MangaERP — Kế Hoạch Hoàn Thiện Hệ Thống

> **Nguồn:** Tổng hợp từ `MangaERP_Tong_Hop_Gap_Analysis.md` + `MangaERP_Tong_Hop_Gap_Analysis (1).md`
> **Cập nhật lần cuối:** 2026-06-30 | **Mục tiêu:** Phân rõ việc cần làm để **demo được** vs **deploy production được**

---

## 📌 Changelog — Tiến Độ Thực Tế

| Ngày | PR / Người | Nội dung | Ghi chú |
|---|---|---|---|
| 2026-06-30 | PR #18 `bach-v2` | `SharedInfrastructureExtensions`: hỗ trợ URI format cho PostgreSQL connection string khi deploy | Không ảnh hưởng local dev |
| 2026-06-30 | PR #19 `nam1` | **3 features mới** (xem chi tiết bên dưới) — merge vào main | ✅ Đã merge vào main |
| 2026-07-01 | PR #25 `bao` | **9 endpoints mới** Phase 1 + Phase 3 MF1 (xem chi tiết bên dưới) — build ✅ 0 errors | ✅ Đã merge vào main |
| 2026-07-02 | PR #26 `nam1` | **Đổi assistant, thành viên studio, deadline, xem layer của Chapter** | ✅ Đã merge vào main |
| 2026-07-02 | PR #27 `bach-v2` | **QA Queue, phân công Fix Task, báo lỗi sửa xong, Hủy lịch xuất bản (MF3)** | ✅ Đã merge vào main |

### ✅ Hoàn thành trong PR #19 / PR #26 `nam1`

| Feature | API | File | Trạng thái |
|---|---|---|---|
| Giao nhiều trang cho Assistant cùng lúc | `POST /api/v1/chapters/{id}/pages/bulk-activate` | `BulkActivatePageTasksHandler.cs` | ✅ Đã merge |
| Duyệt hàng loạt layer của Assistant | `POST /api/v1/tasks/bulk-review` | `BulkReviewLayersHandler.cs` | ✅ Đã merge |
| Xem lịch sử phiên bản layer | `GET /api/v1/tasks/{pageTaskId}/layers` | `GetLayerHistoryHandler.cs` | ✅ Đã merge |

> **Tác động lên plan:** Mục B3 — `GET /api/v1/tasks/{pageTaskId}/layers` đã hoàn thành.

### ✅ Hoàn thành ngày 2026-07-01 — Branch `bao` (PR #25)

| Feature | API | File chính | Trạng thái |
|---|---|---|---|
| Admin Dashboard (User + Submission + Series stats, incl. EiC) | `GET /api/v1/admin/dashboard` | `AdminDashboardController.cs` + `GetAdminDashboardHandler.cs` | ✅ Đã merge |
| Board Reports (rolling 30 ngày) | `GET /api/v1/board/reports` | `BoardController.cs` + `GetBoardReportsHandler.cs` | ✅ Đã merge |
| Xem tất cả series + filter status | `GET /api/v1/series?status=` | `GetAllSeriesHandler.cs` | ✅ Đã merge |
| Cancellation queue cho EB/EiC | `GET /api/v1/series/cancellation-queue` | `GetCancellationQueueHandler.cs` | ✅ Đã merge |
| Mangaka gửi yêu cầu hủy series | `POST /api/v1/series/{id}/request-cancellation` | `RequestCancellationHandler.cs` + Validator | ✅ Đã merge |
| EB/EiC duyệt yêu cầu hủy | `POST /api/v1/series/{id}/approve-cancellation` | `ApproveCancellationHandler.cs` | ✅ Đã merge |
| EB/EiC từ chối yêu cầu hủy | `POST /api/v1/series/{id}/reject-cancellation` | `RejectCancellationHandler.cs` + Validator | ✅ Đã merge |
| Mangaka xóa Draft submission | `DELETE /api/v1/submissions/{id}` | `DeleteDraftHandler.cs` + Validator | ✅ Đã merge |
| EB/EiC/Admin xem phiếu bầu | `GET /api/v1/submissions/{id}/votes` | `GetSubmissionVotesHandler.cs` | ✅ Đã merge |

**Domain mới:** `MangaSeries` — thêm `CancellationRequestStatus` enum + 7 fields + 3 domain methods (`RequestCancellation`, `ApproveCancellation`, `RejectCancellation`)
**EF Migration:** `AddCancellationFieldsToSeries` — cần chạy `dotnet ef database update` trước khi test
**Audit Log:** `GET /api/v1/admin/dashboard` ghi `SystemAuditLog` vào DB (guardrail 1.2)
**Kiến trúc:** Cross-module stats queries (Dashboard/BoardReports) dùng `IMediator.Send()` thay vì inject repository trực tiếp (guardrail 0.3 + 7.5)

> **Tác động lên plan:** Phase 1 (Admin Dashboard) + Phase 3 (Cancellation Flow) + một phần Phase 6 (votes, delete draft) đã hoàn thành.

---

## 🐛 Bugs Cần Fix — Phát Hiện Từ Code Review PR #19

> ⚠️ **Đã hoàn thành sửa toàn bộ bug này.**

### Bug #1 — `BulkReviewLayersHandler.cs` thiếu `SaveChangesAsync` cho PageTask [NGHIÊM TRỌNG]
- **File:** `MangaERP.Task/Application/Commands/BulkReviewLayers/BulkReviewLayersHandler.cs`
- **Người fix:** Nam
- **Trạng thái:** ✅ Đã fix

### Bug #2 — `GetLayerHistoryHandler.cs` xác định Status sai logic [NGHIÊM TRỌNG]
- **File:** `MangaERP.Task/Application/Queries/GetLayerHistory/GetLayerHistoryHandler.cs`
- **Người fix:** Nam
- **Trạng thái:** ✅ Đã fix

### Issue #3 — `GetLayerHistoryHandler.cs` filter `?status=Pending` trả về rỗng không rõ lý do [UX]
- **File:** `MangaERP.Task/Application/Queries/GetLayerHistory/GetLayerHistoryHandler.cs`
- **Người fix:** Nam
- **Trạng thái:** ✅ Đã fix

### Minor #4 — `BulkActivatePageTasksHandler.cs` N+1 queries [PERFORMANCE]
- **File:** `MangaERP.Chapter/Application/Commands/BulkActivatePageTasks/BulkActivatePageTasksHandler.cs`
- **Người fix:** Nam
- **Trạng thái:** ✅ Đã fix

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
| **MF1** — Series Proposal & Vetting | **Bao** branch `bao`| `MangaERP.Submission`, `MangaERP.Series` (cancellation flow), `MangaERP.Studio` (Studio Invitations) |
| **MF2** — Chapter Production & Task | **Nam** (branch `nam1`) | `MangaERP.Chapter`, `MangaERP.Task` |
| **MF3** — QA & Publishing | **Bach** `bach-v2`| `MangaERP.QA`, `MangaERP.Publishing` |
| **Core - Phần 1** (Identity, Security & Notifications) | **Bao** (branch `bao`) | `MangaERP.Identity` (Auth/Profile, Notifications, SignalR, Email, RateLimiter) |
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

| Method | Route | Trang FE chờ | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| GET | `/api/v1/chapters/my-queue` | `ReviewQueuePage.tsx` (Tantou Editor) | 🔴 | ⬜ Chưa làm |
| GET | `/api/v1/assistant/tasks/income` | `AssistantIncomePage.tsx` | 🟢 | ⬜ Chưa làm |

---

## MF3 — Quality Assurance & Publishing `👤 Bach`

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)

| Trang FE | API đã có | Việc FE cần làm |
|---|---|---|
| `ReviewQueuePage.tsx` (Editor/QA) | `GET /api/v1/qa/chapters/{id}/pins`<br>`GET /api/v1/qa/chapters/{id}/session`<br>`POST /api/v1/qa/chapters/{id}/pins`<br>`POST /api/v1/qa/chapters/{id}/send-feedback` | Thêm QA calls vào service layer, dựng canvas ghim lỗi |

### A — API thiếu cần viết mới

| Method | Route | Trang FE chờ | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| GET | `/api/v1/publishing/chapters/my-queue` | `PublishingQueuePage.tsx` (Tantou Editor) | 🔴 | ⬜ Chưa làm |
| GET | `/api/v1/publishing/schedule` | `PublishingSchedulePage.tsx` | 🟡 | ⬜ Chưa làm |
| GET | `/api/v1/qa/queue` | `ReviewQueuePage.tsx` (QA Queue cho Editor nhận Chapter) | 🔴 | ✅ Đã làm (trong `bach-v2`) |

---

## Core — Admin, Notifications & Ranking (Chia theo Owner)

### A — Quick Wins (BE đã có, FE chỉ cần gọi đúng)
> **Người phụ trách:** **Bao** (Core - Phần 1) tích hợp SignalR; cả team wire API vào các dashboard tương ứng.

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

**Checklist BE Ranking (Phân rã chi tiết cho Bach để tăng commits/lines):**
- [ ] Commit 1: Tạo DB entities, EF Configurations và chạy Migration tạo bảng cho `VoteData` & `RankingSnapshot` (**Bach**)
- [ ] Commit 2: Triển khai Infrastructure Repositories cho Ranking (**Bach**)
- [ ] Commit 3: Viết Command `ImportVoteDataCommand` + Validator nhập phiếu thô (**Bach**)
- [ ] Commit 4: Viết Command `CompileRankingCommand` chứa thuật toán tổng hợp điểm và xếp hạng truyện (**Bach**)
- [ ] Commit 5: Viết Query `GetRankingBoardQuery` và `GetRankingPeriodsQuery` (**Bach**)
- [ ] Commit 6: Viết Controller `RankingController.cs` expose toàn bộ API (**Bach**)
- [ ] Commit 7-8: Viết bộ Unit Tests cho Ranking (`RankingTests.cs` tối thiểu 200 dòng) (**Bach**)

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

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|---|
| PUT | `/api/v1/series/{id}` | Mangaka | Không sửa được metadata sau khi approved | 🟡 | ⬜ Chưa làm |
| POST | `/api/v1/series/{id}/set-hiatus` | Mangaka, Admin | Không có trạng thái tạm nghỉ | 🟢 | ⬜ Chưa làm |
| POST | `/api/v1/series/{id}/reactivate` | Mangaka, Admin | Không khôi phục từ Hiatus được | 🟢 | ⬜ Chưa làm |
| GET | `/api/v1/studios/{seriesId}/members` | Mangaka, TE | Không biết ai đang trong studio | 🟢 | ✅ Đã làm (trong `nam1`) |
| DELETE | `/api/v1/studios/{seriesId}/members/{assistantId}` | Mangaka | Không khai trừ được assistant (Yêu cầu side-effect: thu hồi tasks chưa xong của assistant & notify) | 🟢 | ⬜ Chưa làm |
| POST | `/api/v1/studios/invitations/{id}/cancel` | Mangaka | Gửi nhầm lời mời không thu hồi được | 🟢 | ✅ Đã làm (trong `nam1`) |

### B — Quản lý Chapter & Trang vẽ

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|---|
| PUT | `/api/v1/chapters/{id}` | Mangaka | Nhập sai tiêu đề/trang không sửa được | 🟢 | ⬜ Chưa làm |
| DELETE | `/api/v1/chapters/{id}` | Mangaka | Chapter rác tồn tại vĩnh viễn | 🟢 | ⬜ Chưa làm |
| GET | `/api/v1/chapters/{id}/pages` | Mangaka, TE | Không quản lý danh sách trang vẽ của chapter | 🟡 | ✅ Đã làm (trong `nam1` & fixed auth bug) |
| PATCH | `/api/v1/chapters/{id}/pages/{pageNum}/reassign` | Mangaka | Không đổi assistant cho trang cụ thể | 🟢 | ✅ Đã làm (trong `nam1` - `PUT /reassign`) |

### B — Task & Layer

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|---|
| GET | `/api/v1/tasks/{pageTaskId}` | Mangaka, Assistant | Không xem được chi tiết 1 task | 🟡 | ✅ Đã làm (trong `nam1`) |
| PATCH | `/api/v1/tasks/{pageTaskId}/deadline` | Mangaka | Không cập nhật được deadline khi tiến độ thay đổi | 🟢 | ✅ Đã làm (trong `nam1` - `PUT /deadline`) |
| GET | `/api/v1/studios/{seriesId}/tasks/board` | Mangaka, TE, Assistant | Không hiển thị được giao diện Kanban Board phân nhóm task trong Studio | 🟢 | ⬜ Chưa làm |
| GET | `/api/v1/tasks/{pageTaskId}/layers/{layerType}/versions` | Mangaka, Assistant | Không xem lại được các bản vẽ cũ của Assistant (Lịch sử phiên bản layer) | 🟢 | ⬜ Chưa làm |
| POST | `/api/v1/tasks/{pageTaskId}/layers/{layerType}/rollback` | Mangaka | Không đảo ngược (rollback) layer về bản vẽ cũ khi Assistant vẽ sai | 🟢 | ⬜ Chưa làm |

---

## MF3 — Quality Assurance & Publishing `👤 Bach`

### B — QA History & Pins

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|---|
| GET | `/api/v1/qa/chapters/{id}/history` | TE, Mangaka, Admin | Không truy vết lịch sử QA (Yêu cầu log timeline chi tiết: ghim lỗi, sửa, duyệt...) | 🟢 | ⬜ Chưa làm |
| POST | `/api/v1/qa/chapters/{id}/reopen` | TE | Không mở lại được QA nếu phát hiện sót lỗi (Yêu cầu cập nhật status & notify liên quan) | 🟢 | ⬜ Chưa làm |
| PATCH | `/api/v1/qa/pins/{pinId}` | TE | Không sửa được nội dung pin tạo nhầm | 🟢 | ⬜ Chưa làm |
| DELETE | `/api/v1/qa/pins/{pinId}` | TE | Không xóa được pin tạo nhầm | 🟢 | ⬜ Chưa làm |
| POST | `/api/v1/qa/pins/{pinId}/fixed` | Mangaka/Assistant | Không có nút báo cáo đã sửa lỗi (Bug pin status lifecycle) | 🟡 | ✅ Đã làm (trong `bach-v2`) |
| POST | `/api/v1/qa/pins/{pinId}/assign-fix` | Mangaka | Không có API phân công Fix Task cho Assistant | 🟡 | ✅ Đã làm (trong `bach-v2`) |

### B — Publishing Schedule

| Method | Route | Người dùng | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|---|
| GET | `/api/v1/publishing/chapters/{id}` | EB, Admin, TE | Không xem được chi tiết trạng thái phát hành | 🟢 | ⬜ Chưa làm |
| PATCH | `/api/v1/publishing/schedule/{id}` | EB | Lên lịch sai ngày không sửa được | 🟢 | ✅ Đã làm (trong `bach-v2` - `PATCH /schedule`) |
| DELETE | `/api/v1/publishing/schedule/{id}` | EB | Lên lịch nhầm phải nhờ Admin can thiệp DB | 🟢 | ✅ Đã làm (trong `bach-v2` - `DELETE /schedule`) |

---

## Core — Identity, Notifications & Infrastructure (Chia theo Owner)

### B — Identity & Tài Khoản (Người làm: **Bao** — Core - Phần 1)

| Method | Route | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| GET | `/api/v1/users/me` | FE không biết tên/avatar/role sau login — phải giải mã JWT thủ công | 🔴 | ⬜ Chưa làm |
| PUT | `/api/v1/users/me/change-password` | Không tự đổi được mật khẩu — phải nhờ Admin reset | 🟢 | ⬜ Chưa làm |
| POST | `/api/v1/auth/forgot-password` | Quên mật khẩu → không có cách vào lại tài khoản | 🟢 | ⬜ Chưa làm |
| POST | `/api/v1/auth/reset-password` | Đi kèm forgot-password | 🟢 | ⬜ Chưa làm |
| PUT | `/api/v1/users/me/avatar` | Mọi user dùng avatar mặc định, không cá nhân hóa | 🟢 | ⬜ Chưa làm |

> `POST /auth/logout` và `POST /auth/refresh` **đã có** — bỏ qua tài liệu cũ ghi là "thiếu".

### B — Notifications (Người làm: **Bao** — Core - Phần 1)

| Method | Route | Vấn đề nếu thiếu | Ưu tiên | Trạng thái |
|---|---|---|---|---|
| GET | `/api/v1/notifications/unread-count` | Không có badge số chưa đọc trên Navbar | 🟡 | ⬜ Chưa làm |
| DELETE | `/api/v1/notifications/{id}` | Không dọn được thông báo đơn lẻ | 🟢 | ⬜ Chưa làm |
| DELETE | `/api/v1/notifications` | Không xóa hàng loạt thông báo đã đọc | 🟢 | ⬜ Chưa làm |

### B — Infrastructure & Services (Bắt buộc trước go-live)

| Service | Tình trạng hiện tại | Hậu quả nếu thiếu | Ưu tiên | Người làm BE |
|---|---|---|---|---|
| **SignalR Hub** | Logic có nhưng Hub chưa đăng ký endpoint | Notification chỉ thấy khi refresh trang, không real-time | ⚪ | **Bao** (Core - Phần 1) |
| **Scheduled Publisher Job** | Lưu DB nhưng không có job trigger | Chapter lên lịch không bao giờ tự phát hành | ⚪ | **Bach** (Core - Phần 2) |
| **File Storage (S3/Blob)** | FE tự nhập URL thủ công | Không upload được ảnh bìa, bản thảo, layer vẽ | ⚪ | **Bao** (Core - Phần 3) |
| **Email Service** | Chỉ log ra console | Email kích hoạt & reset password không đến được user | ⚪ | **Bao** (Core - Elevate) |
| **Global Exception Handler** | Mỗi controller tự try-catch riêng | FE nhận HTML error page thay vì JSON → app crash phía client | ⚪ | **Bao** (Core - Phần 3) |
| **Audit Log** | Không có | Không điều tra được sự cố — ai làm gì, lúc mấy giờ | ⚪ | **Bao** (Core - Phần 3) |
| **Rate Limiter** | Không có | Dễ bị spam login/vote — lỗ hổng bảo mật | ⚪ | **Bao** (Core - Phần 1) |
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
| **Phase 1 — Fix Bugs & Unblock Core** | Fix Bug #1 và #2 từ PR #19; viết `GET /users/me` (**Bao**); viết 2 queue TE (`/chapters/my-queue`, `/publishing/chapters/my-queue`); viết Admin Dashboard (**Bao**) | Core + MF2 + MF3 | 🔴 | 🔄 Một phần: Admin Dashboard ✅ done Bao, Bugs #1 #2 + QA queue ✅ done Nam/Bach. Còn thiếu: users/me, publishing queue ⬜ |
| **Phase 2 — Ranking Module** | Toàn bộ Infrastructure + Application + Controller Ranking (Phương án A thủ công) (**Bach**) | Core | 🟡 Khối lượng lớn nhất | ⬜ Chưa bắt đầu |
| **Phase 3 — Cancellation Flow** | request/approve/reject-cancellation đúng phân quyền EB/EIC; cancellation-queue; board/reports | MF1 | 🟡 | ✅ Hoàn thành (2026-07-01, Bao) |
| **Phase 4 — CRUD Hoàn Chỉnh** | PUT/DELETE series, chapter; Studio members; Reassign trang; Task deadline; QA pins; Publishing schedule | MF1 + MF2 + MF3 | 🟡–🟢 | 🔄 Một phần: Studio members, Reassign trang, Task deadline, QA pins (assign/fixed), Publishing schedule (schedule/update/cancel) ✅. PUT/DELETE series/chapter/pins ⬜ chưa |
| **Phase 5 — Identity & Notifications** | change-password, forgot/reset-password, avatar; notifications unread-count, delete (**Bao**) | Core | 🟢 | ⬜ Chưa bắt đầu |
| **Phase 6 — History & Transparency** | submissions/{id}/votes; QA history/reopen; tasks/{id} detail; publishing/{id} detail | MF1 + MF2 + MF3 | 🟢 | 🔄 Một phần: votes + delete draft ✅ done Bao, tasks/publishing detail ✅ done Nam/Bach. QA history/reopen ⬜ chưa |
| **Phase 7 — Production Infrastructure** | SignalR Hub, Email SMTP, Rate Limiter (**Bao**) <br> Scheduled Publisher Job, Health Check (**Bach**) <br> File Storage thật, Global Exception Handler, Audit Log (**Bao**) | Infra | ⚪ Bắt buộc trước go-live | 🔄 Một phần: Audit Log cho Admin Dashboard ✅. Còn lại ⬜ |
| **Phase 8 — Optional Extensions (Backlog)** | Các tính năng thảo luận task, gợi ý assistant (**Nam**); severity/categories cho QA pin, check xung đột lịch xuất bản (**Bach**) | Core / MF2 / MF3 | 🟢 Tùy chọn tăng commits | ⬜ Chưa bắt đầu |

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
| Bug #1 | `BulkReviewLayersHandler.cs` — thiếu `_pageTaskRepo.SaveChangesAsync` | 🔴 Nghiêm trọng | Nam | ✅ Đã fix |
| Bug #2 | `GetLayerHistoryHandler.cs` — status suy luận từ `RejectionNote` sai | 🔴 Nghiêm trọng | Nam | ✅ Đã fix |
| Issue #3 | `GetLayerHistoryHandler.cs` — `?status=Pending` trả `[]` không báo lỗi | 🟡 Minor UX | Nam | ✅ Đã fix |
| Minor #4 | `BulkActivatePageTasksHandler.cs` — N+1 queries khi giao nhiều trang | 🟢 Performance | Nam | ✅ Đã fix |

---

## 📌 Lộ Trình Hoàn Thiện Module Segmentation (SAM) — Phase 3-6

### Phase 3 — Invert Mask (Quyết định kỹ thuật: Làm ở FE)
- **FE:** Decode RLE → Invert trên canvas phụ (bằng XOR composite hoặc pixel manipulation) → Re-encode RLE và gửi lên API khi giao việc.
- **BE:** Không cần thay đổi gì.

### Phase 4 — Khóa Vùng Vẽ Cho Assistant (FE + BE `👤 Bao`)
- **FE:** Gọi `GET /api/segmentation/tasks/mine?status=Pending` → Decode RLE → Dựng clip mask chặn vẽ ngoài vùng.
- **BE:** ✅ **Đã hoàn thành** — Thêm thông tin kích thước ảnh gốc `OriginalWidth`/`OriginalHeight` vào `SegmentationTaskDto` và thực hiện tạo migration `AddImageSizeToSegmentationTask` để lưu trực tiếp lúc upload (không đọc lại file lúc query). Endpoint `GET /api/segmentation/tasks/mine` đã tích hợp phân trang chuẩn.

### Phase 5 — Content Moderation Pipeline (BE `👤 Bao`)
- **BE (S3/File Storage):** Sau khi ảnh được upload, gọi `SamServiceClient` chạy `/embedding` để quét xem ảnh có hợp lệ/không bị hỏng trước khi cho phép publish.
- *Giới hạn:* Chỉ kiểm tra tính hợp lệ cơ bản của ảnh, không phải bộ lọc NSFW đầy đủ. Nếu Colab sập, chỉ ghi log cảnh báo và bỏ qua (không chặn luồng chính).

### Phase 6 — Test & Rollout (BE `👤 Bao`)
- Test tích hợp Circuit Breaker khi Colab ngắt kết nối.
- Chạy 3 lệnh grep kiểm tra cách ly module (Segmentation cô lập hoàn toàn khỏi Task, Chapter, Identity).
- Chạy `dotnet ef database update` cho Segmentation DbContext để đồng bộ bảng vật lý lên DB dev thật.

### 🔗 Tích Hợp Hệ Thống Event (BE — MF2 & Core 1)
- **Tình trạng:** ✅ **Đã hoàn thành (2026-07-02)**
- **Triển khai:**
  - `INotificationService` — thêm method `NotifySegmentationTaskAssignedAsync(assistantId, segmentationTaskId, taskType)`
  - `NotificationService.cs` — implement: lưu DB + push SignalR `ReceiveNotification` tới `assistantId`
  - `SegmentationTaskAssignedHandler.cs` (module `MangaERP.Task`) — `INotificationHandler<SegmentationTaskAssignedEvent>` lắng nghe sự kiện, gọi `NotifySegmentationTaskAssignedAsync`; lỗi notification bị swallow (không rollback transaction Segmentation)
- **Build:** ✅ 0 errors

---

## ➕ Các Tính Năng Bổ Trợ (Phần Hoàn Thiện Sau Cùng - Backlog Tùy Chọn Tăng Commits/Lines)

> 💡 **Mục đích:** Tập trung hoàn thành Phase 1 - 7 trước để chạy ổn định luồng nghiệp vụ chính. Các tính năng này được xếp vào Backlog (Phase 8), nhóm sẽ phát triển sau khi hệ thống cốt lõi đã sẵn sàng.

### 👤 Cho Nam (MF2 - Task & Studio)
- **Task Comments (Thảo luận trên trang vẽ)**:
  - `POST /api/v1/tasks/{id}/comments` (Thêm bình luận mới)
  - `GET /api/v1/tasks/{id}/comments` (Lấy danh sách trao đổi ý kiến của trang vẽ)
- **Assistant Recommendation (Gợi ý phân công tự động)**:
  - `GET /api/v1/chapters/{id}/recommend-assistants?pageNum=X` (Thuật toán gợi ý người vẽ phù hợp dựa trên workload hiện tại và phần mềm sử dụng).

### 👤 Cho Bach (MF3 - QA & Publishing)
- **QA Pin Severity & Categories (Phân loại & Mức độ lỗi)**:
  - Thêm thuộc tính `Severity` (Blocker, Major, Minor) và `Category` (LineArt, Coloring, Text) vào `QaPin`. Ngăn chặn xuất bản chapter nếu tồn tại lỗi ở mức `Blocker`.
- **Publishing Conflict Checker (Kiểm soát xung đột lịch phát hành)**:
  - Tích hợp logic kiểm tra ngày giờ xuất bản của chapter mới so với các chapter trước để tránh lỗi đảo lộn thứ tự chapter hiển thị.

### 👤 Cho Bao (Core, Infra & MF1 Business Improvements)
- **Token Blacklist Service (Bảo mật Logout)**:
  - Triển khai `TokenBlacklistService` sử dụng cache để vô hiệu hóa ngay lập tức các JWT Access Token đã đăng xuất trước khi chúng hết hạn.
- **Rate Limiting Policies (Giới hạn request phân tầng)**:
  - Phân tách chính sách giới hạn request giữa API thông thường và API nhạy cảm (như Login, Reset Password để chống tấn công brute-force).
- **Auto-Resolution for Vetting (Tự động quyết định duyệt truyện khi hết hạn)**:
  - Viết background job quét các đề xuất truyện (Submission) quá hạn bỏ phiếu (ví dụ: 7 ngày). Nếu hết hạn, tự động giải quyết trạng thái (Resolve) dựa trên số phiếu đa số hiện tại và gửi thông báo cho tác giả.
- **Submission Revisions (Ghi nhận phiên bản hiệu chỉnh đề xuất)**:
  - Hỗ trợ Mangaka gửi lại bản thảo đã sửa đổi (`POST /api/v1/submissions/{id}/revisions`) sau khi bị từ chối thay vì phải tạo mới, giúp giữ nguyên lịch sử vote và ý kiến đánh giá trước đó của Biên tập viên.
- **Dynamic Image Optimization (Tối ưu hóa ảnh tự động qua CDN)**:
  - Bổ sung logic tự động chuyển hướng/sửa đổi URL trả về từ Cloudinary để áp dụng định dạng WebP nén tối ưu (`q_auto,f_auto`), giảm tải dung lượng khi FE hiển thị các trang truyện nặng.
