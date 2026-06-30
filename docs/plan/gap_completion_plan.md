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

---

# PHẦN A — GAP: Frontend ↔ Backend

## A0. ✅ Quick Wins — BE đã có, chỉ cần FE kết nối (không cần code BE)

> **Ưu tiên cao nhất.** 9 trang đang hiện `EmptyBackendState` dù API đã sẵn sàng.

| # | Trang FE | API cần gọi | Việc FE cần làm |
|---|---|---|---|
| 1 | `AdminNotificationsPage` | `GET /api/v1/notifications` | Xóa `EmptyBackendState`, thêm call vào `mangaErpService.ts` |
| 2 | `BoardNotificationsPage` | `PATCH /api/v1/notifications/{id}/read` | Như trên |
| 3 | `editors/NotificationsPage` | `PATCH /api/v1/notifications/read-all` | Như trên + kết nối SignalR `/hubs/notifications` |
| 4 | Board Voting Center | `GET /api/v1/submissions/queue` | Hợp nhất route `/app/board/voting-center` → `SeriesProposalsPage.tsx` |
| 5 | Board Dashboard (MF1) | `POST /api/v1/submissions/{id}/vote`, `.../resolve-conflict` | Wire API vào component |
| 6 | Editor Dashboard (Tantou) | `GET /api/v1/series` (filter `ManagingTantouId` từ JWT) | Thay mock data bằng API thật |
| 7 | `SeriesMonitoringPage.tsx` (Editor) | `GET /api/v1/chapters?seriesId=...` | Như trên |
| 8 | `ReviewQueuePage.tsx` (Editor/QA) | `GET /api/v1/qa/chapters/{id}/pins`, `.../session` | Thêm QA service calls, dựng canvas ghim lỗi |
| 9 | Assistant pages | `GET /api/v1/tasks/assigned`, `POST /api/v1/tasks/{id}/submit-layer` | Đã đủ — chỉ cần FE wire vào |

**⚠️ Riêng trang `AdminSeriesMonitoringPage.tsx`:** BE cần bổ sung quyền Admin xem toàn bộ + query param `?status=Active|Cancelled|Hiatus`.

---

## A1. 🔨 API thật sự thiếu — cần viết mới để trang FE hết trắng

### A1.1 Admin APIs
| Method | Route | Trang FE chờ | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/admin/dashboard` | `AdminDashboardPage.tsx` | 🔴 |
| GET | `/api/v1/admin/workflow-stats` | `AdminWorkflowMonitoringPage.tsx` | 🟡 |
| GET | `/api/v1/admin/reports` | `AdminReportsAnalyticsPage.tsx` | 🟡 |
| GET | `/api/v1/admin/roles` | `AdminRolesPage.tsx` | 🟢 |

### A1.2 TE/Editor Queue APIs
| Method | Route | Trang FE chờ | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/chapters/my-queue` | `ReviewQueuePage.tsx` | 🔴 |
| GET | `/api/v1/publishing/chapters/my-queue` | `PublishingQueuePage.tsx` | 🔴 |
| GET | `/api/v1/publishing/schedule` | `PublishingSchedulePage.tsx` | 🟡 |

### A1.3 Board & Cancellation APIs
| Method | Route | Trang FE chờ | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/series/cancellation-queue` | `CancellationReviewPage.tsx` | 🟡 |
| GET | `/api/v1/board/reports` | `ReportsPage.tsx` (Board) | 🟡 |

### A1.4 Assistant Income API
| Method | Route | Trang FE chờ | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/assistant/tasks/income` | `AssistantIncomePage.tsx` | 🟢 |

### A1.5 Ranking Module — khối lượng lớn nhất, toàn bộ cần viết mới

> Hiện chỉ có Domain entities (`VoteData`, `RankingSnapshot`). Chưa có Application / Infrastructure / Controller.

| Method | Route | Vai trò | Mô tả |
|---|---|---|---|
| POST | `/api/v1/ranking/import` | Admin, EB | Import phiếu bầu thô theo kỳ |
| POST | `/api/v1/ranking/compile` | Admin, EB | Gom vote, gán Rank, lưu Snapshot |
| GET | `/api/v1/ranking/board?period=...` | Public | Bảng xếp hạng chính thức |
| GET | `/api/v1/ranking/periods` | Public | Danh sách kỳ đã có snapshot |
| GET | `/api/v1/ranking/import/{period}` | Admin, EB | Xem phiếu thô đã import |
| DELETE | `/api/v1/ranking/import/{period}` | Admin | Xóa phiếu thô trước khi compile |

**Checklist backend Ranking (Phương án A — thủ công):**
- [ ] Infrastructure: `IVoteDataRepository`, `IRankingSnapshotRepository`, EF mapping, migration
- [ ] Application: `ImportVoteDataCommand`, `CompileRankingCommand`, `GetRankingBoardQuery`
- [ ] Presentation: `RankingController.cs`

---

# PHẦN B — GAP: System Completeness (Production-Ready)

> Không có trang FE nào báo lỗi vì mục này, nhưng **người dùng thật sẽ bị stuck ngay tuần đầu**.

## B1. 🔐 Identity & User Management

| Method | Route | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/users/me` | FE không biết tên/avatar/role sau login — phải giải mã JWT thủ công | 🔴 |
| PUT | `/api/v1/users/me/change-password` | Không tự đổi được mật khẩu | 🟢 |
| POST | `/api/v1/auth/forgot-password` | Quên mật khẩu → không vào lại được | 🟢 |
| POST | `/api/v1/auth/reset-password` | Đi kèm forgot-password | 🟢 |
| PUT | `/api/v1/users/me/avatar` | Mọi user dùng avatar mặc định | 🟢 |

> `POST /auth/logout` và `POST /auth/refresh` **đã có** — bỏ qua các tài liệu cũ ghi là "thiếu".

## B2. 📝 CRUD Vòng Đời Đầy Đủ

### Series
| Method | Route | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|
| PUT | `/api/v1/series/{id}` | Mangaka không sửa được metadata | 🟡 |
| POST | `/api/v1/series/{id}/request-cancellation` | Luồng Cancellation đứt ngay bước đầu | 🟡 |
| POST | `/api/v1/series/{id}/approve-cancellation` | EIC/EB không phê duyệt được | 🟡 |
| POST | `/api/v1/series/{id}/reject-cancellation` | Series kẹt ở trạng thái chờ | 🟡 |
| POST | `/api/v1/series/{id}/set-hiatus` | Không có trạng thái tạm nghỉ | 🟢 |
| POST | `/api/v1/series/{id}/reactivate` | Không khôi phục từ Hiatus được | 🟢 |

> ⚠️ **Nghiệp vụ Cancellation:** Quyết định hủy thuộc EB/EIC, không phải Admin. `MangaSeries.Cancel()` đã có nhưng sai phân quyền và thiếu workflow request→approve/reject.

### Chapter & Task
| Method | Route | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|
| PUT | `/api/v1/chapters/{id}` | Nhập sai không sửa được | 🟢 |
| DELETE | `/api/v1/chapters/{id}` | Chapter rác tồn tại vĩnh viễn | 🟢 |
| DELETE | `/api/v1/submissions/{id}` | Draft nhầm không xóa được | 🟢 |
| GET | `/api/v1/chapters/{id}/pages` | Không quản lý được danh sách trang | 🟡 |
| PATCH | `/api/v1/chapters/{id}/pages/{pageNum}/reassign` | Không đổi người vẽ trang được | 🟢 |
| PATCH | `/api/v1/tasks/{pageTaskId}/deadline` | Không cập nhật deadline | 🟢 |

### Publishing Schedule
| Method | Route | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|
| PATCH | `/api/v1/publishing/schedule/{id}` | Lên lịch sai không sửa được | 🟢 |
| DELETE | `/api/v1/publishing/schedule/{id}` | Lên lịch nhầm phải nhờ Admin can DB | 🟢 |

### Studio & Notifications
| Method | Route | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/studios/{seriesId}/members` | Không biết ai trong studio | 🟢 |
| DELETE | `/api/v1/studios/{seriesId}/members/{assistantId}` | Không khai trừ được assistant | 🟢 |
| POST | `/api/v1/studios/invitations/{id}/cancel` | Gửi nhầm lời mời không thu hồi được | 🟢 |
| DELETE | `/api/v1/notifications/{id}` | Không dọn thông báo cũ | 🟢 |
| DELETE | `/api/v1/notifications` | Xóa hàng loạt thông báo đã đọc | 🟢 |
| GET | `/api/v1/notifications/unread-count` | Không có badge số chưa đọc trên navbar | 🟡 |

## B3. 🔍 History & Transparency

| Method | Route | Vấn đề nếu thiếu | Ưu tiên |
|---|---|---|---|
| GET | `/api/v1/submissions/{id}/votes` | EB/EIC không xem được ai vote gì | 🟢 |
| GET | `/api/v1/qa/chapters/{id}/history` | Không truy vết lịch sử QA | 🟢 |
| POST | `/api/v1/qa/chapters/{id}/reopen` | Không mở lại QA nếu sót lỗi | 🟢 |
| PATCH | `/api/v1/qa/pins/{pinId}` | Không sửa pin tạo nhầm | 🟢 |
| DELETE | `/api/v1/qa/pins/{pinId}` | Không xóa pin tạo nhầm | 🟢 |
| GET | `/api/v1/tasks/{pageTaskId}` | Không xem chi tiết task | 🟡 |
| ~~GET~~ | ~~`/api/v1/tasks/{pageTaskId}/layers`~~ | ~~Không xem lịch sử layer~~ | ✅ **Đã có** (PR #19 nam1) — nhưng có Bug #2 cần fix |
| GET | `/api/v1/publishing/chapters/{id}` | Không xem chi tiết trạng thái phát hành | 🟢 |

## B4. 🔧 Infrastructure & Services

| Service | Tình trạng | Hậu quả nếu thiếu | Ưu tiên |
|---|---|---|---|
| **SignalR Hub** | Logic có nhưng Hub chưa đăng ký endpoint | Notification chỉ thấy khi refresh trang | ⚪ |
| **Scheduled Publisher Job** | Lưu DB nhưng không có job trigger | Chapter lên lịch không bao giờ tự phát hành | ⚪ |
| **File Storage (S3/Blob)** | FE nhập URL thủ công | Không upload được ảnh, bản thảo, layer | ⚪ |
| **Email Service** | Chỉ log console | Email kích hoạt & reset password không đến được user | ⚪ |
| **Global Exception Handler** | Mỗi controller tự try-catch | FE nhận HTML error thay vì JSON → app crash | ⚪ |
| **Audit Log** | Không có | Không điều tra được sự cố | ⚪ |
| **Rate Limiter** | Không có | Dễ spam login/vote | ⚪ |
| **Health Check** | Không có `GET /health` | Monitoring không biết service còn sống | ⚪ |

---

# 📅 Lộ Trình Triển Khai

| Phase | Nội dung | GAP | Thời gian ước tính |
|---|---|---|---|
| **Phase 0 — Quick Wins** | Kết nối 9 trang FE đang gọi sai (không cần code BE mới) | A0 | 1–2 ngày |
| **Phase 1 — Unblock Core Demo** | `GET /users/me`, `chapters/my-queue`, `publishing/my-queue`, Admin Dashboard, `notifications/unread-count` | A1 + B1 | 3–4 ngày |
| **Phase 2 — Ranking Module** | Toàn bộ Infrastructure + Application + Controller (Phương án A) | A1.5 | 5–7 ngày |
| **Phase 3 — Cancellation Flow** | request/approve/reject-cancellation đúng phân quyền EB/EIC | A1.3 + B2 | 2–3 ngày |
| **Phase 4 — CRUD Hoàn Chỉnh** | PUT/DELETE series, chapter, schedule, studio, QA pins, notifications | B2 | 3–5 ngày |
| **Phase 5 — Identity & Security** | change-password, forgot/reset-password, avatar | B1 | 2 ngày |
| **Phase 6 — History & Transparency** | submission votes, QA history/reopen, task layers | B3 | 2–3 ngày |
| **Phase 7 — Production Infrastructure** | SignalR, Scheduled Publisher, File Storage, Email thật, Exception Handler, Audit Log, Rate Limiter, Health Check | B4 | 7–10 ngày |

---

## 🚫 Ngoài Phạm Vi (Không làm giai đoạn này)

- `/admin/ai`, `/admin/storage` — giữ `EmptyBackendState` hoặc ẩn menu
- `DiscoverPage`, `TrendingPage`, `GenresPage` — dùng mock data
- Reports nâng cao riêng — tái dùng số liệu từ Admin Dashboard + Ranking

---

## 📊 Tổng Kết

| Hạng mục | Số lượng |
|---|---|
| ✅ API BE đã có, FE chưa gọi đúng | 9 trang |
| 🔨 API cần viết mới (GAP A — để demo) | ~20 endpoints |
| 🔨 API cần viết mới (GAP B — production) | ~27 endpoints (trừ 1 đã xong) |
| 🔧 Service/Infrastructure thiếu | 8 items |
| 🆕 API mới hoàn thành (PR #19) | 3 endpoints |
| 🐛 Bugs cần fix từ code review | 2 nghiêm trọng, 1 minor, 1 performance |

> **Rule of thumb:** Phase 0–2 = đủ để demo. Phase 3–7 = đủ để deploy thật.

---

## 📋 Checklist Bugs Cần Fix (Team Action Items)

| # | File | Mức độ | Người fix | Trạng thái |
|---|---|---|---|---|
| Bug #1 | `BulkReviewLayersHandler.cs` — thiếu `_pageTaskRepo.SaveChangesAsync` | 🔴 Nghiêm trọng | _(chưa assign)_ | ⬜ Chưa fix |
| Bug #2 | `GetLayerHistoryHandler.cs` — status suy luận từ `RejectionNote` sai | 🔴 Nghiêm trọng | _(chưa assign)_ | ⬜ Chưa fix |
| Issue #3 | `GetLayerHistoryHandler.cs` — `?status=Pending` trả `[]` không rõ lý do | 🟡 Minor UX | _(chưa assign)_ | ⬜ Chưa fix |
| Minor #4 | `BulkActivatePageTasksHandler.cs` — N+1 queries khi giao nhiều trang | 🟢 Performance | _(chưa assign)_ | ⬜ Có thể để sau |
