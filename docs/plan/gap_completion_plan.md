# 🗺️ MangaERP — Kế Hoạch Hoàn Thiện Hệ Thống

> **Cập nhật lần cuối:** 2026-07-07  
> **Bố cục:** Chia theo người phụ trách — mỗi người có 2 mục: **Gap với FE** (BE đã có, FE chưa gọi / gọi sai) và **Gap với Production** (BE chưa làm hoặc chưa hoàn chỉnh).

---

## 📌 Changelog — Tiến Độ Thực Tế

| Ngày | PR / Người | Nội dung | Ghi chú |
|---|---|---|---|
| 2026-06-30 | PR #18 `bach-v2` | `SharedInfrastructureExtensions`: hỗ trợ URI format cho PostgreSQL connection string khi deploy | Không ảnh hưởng local dev |
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

---

## 🚫 Ngoài Phạm Vi (Không làm giai đoạn này)

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
