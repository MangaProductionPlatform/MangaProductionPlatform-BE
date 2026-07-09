# 📡 Toàn Bộ API — Đang Có & Còn Thiếu
> **Phiên bản:** 2026-06-30 | Mục tiêu: Hệ thống vận hành như production thật  
> **Ký hiệu:** ✅ Đã có | 🔨 Cần viết mới | ⚠️ Có nhưng chưa đủ | 🚫 Ngoài scope

---

## 📦 MODULE: Identity & Auth

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| POST | `/api/v1/auth/login` | Public | Đăng nhập, trả JWT + refresh cookie |
| POST | `/api/v1/auth/logout` | Public | Đăng xuất, xóa refresh cookie |
| POST | `/api/v1/auth/refresh` | Public | Làm mới access token qua cookie |
| POST | `/api/v1/auth/activate` | Public | Kích hoạt tài khoản qua token email |
| PUT  | `/api/v1/users/profile` | All | Cập nhật thông tin cá nhân (PenName, Bank) |
| POST | `/api/v1/admin/accounts/provision` | Admin | Tạo tài khoản nội bộ |
| GET  | `/api/v1/admin/accounts` | Admin | Danh sách tài khoản, lọc theo role/status |
| GET  | `/api/v1/admin/accounts/{userId}` | Admin | Xem chi tiết 1 tài khoản |
| PATCH| `/api/v1/admin/accounts/{userId}/role` | Admin | Đổi role |
| PATCH| `/api/v1/admin/accounts/{userId}/status` | Admin | Đổi trạng thái (Suspend/Activate) |
| PUT  | `/api/v1/admin/accounts/{userId}` | Admin | Cập nhật thông tin tài khoản |
| POST | `/api/v1/admin/accounts/{userId}/resend-activation` | Admin | Gửi lại email kích hoạt |
| DELETE | `/api/v1/admin/accounts/{userId}` | Admin | Xóa tài khoản |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| GET | `/api/v1/users/me` | All | Lấy thông tin profile của chính mình | FE cần hiển thị tên, avatar, role sau khi login |
| PUT | `/api/v1/users/me/avatar` | All | Upload/cập nhật ảnh đại diện | Profile page của mọi role |
| POST | `/api/v1/auth/forgot-password` | Public | Quên mật khẩu — gửi link reset qua email | Quy trình identity cơ bản |
| POST | `/api/v1/auth/reset-password` | Public | Đặt lại mật khẩu qua token | Đi kèm forgot-password |
| PUT | `/api/v1/users/me/change-password` | All | Tự đổi mật khẩu khi đã đăng nhập | Security cơ bản |
| GET | `/api/v1/admin/dashboard` | Admin | Thống kê tổng quan hệ thống | AdminDashboardPage |
| GET | `/api/v1/admin/roles` | Admin | Danh sách static roles + mô tả | AdminRolesPage |
| GET | `/api/v1/admin/workflow-stats` | Admin | Thống kê trạng thái vận hành các luồng | AdminWorkflowMonitoringPage |
| GET | `/api/v1/admin/reports` | Admin | Báo cáo tăng trưởng series/chapter theo tháng | AdminReportsAnalyticsPage |

---

## 📦 MODULE: Series

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| GET | `/api/v1/series/my` | Mangaka | Danh sách series của mình |
| GET | `/api/v1/series/{id}` | All staff | Chi tiết 1 series |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| GET | `/api/v1/series` | Admin, TE, EB | Danh sách tất cả series, có filter `?status=Active\|Cancelled\|Hiatus` | AdminSeriesMonitoringPage, Editor workspace |
| PUT | `/api/v1/series/{id}` | Mangaka | Sửa metadata series (Title, Genre, CoverImage) | Chỉnh sửa thông tin sau khi approved |
| POST | `/api/v1/series/{id}/request-cancellation` | Mangaka | Gửi yêu cầu hủy bộ truyện kèm lý do | Luồng Cancellation |
| GET | `/api/v1/series/cancellation-queue` | EB, EIC, Admin | Danh sách yêu cầu hủy đang chờ duyệt | CancellationReviewPage (Board) |
| POST | `/api/v1/series/{id}/approve-cancellation` | EIC, EB | Phê duyệt hủy — chuyển status → Cancelled | Phân quyền đúng vai trò |
| POST | `/api/v1/series/{id}/reject-cancellation` | EIC, EB | Từ chối yêu cầu hủy | Phân quyền đúng vai trò |
| POST | `/api/v1/series/{id}/set-hiatus` | Mangaka, Admin | Chuyển sang trạng thái tạm nghỉ | Nghiệp vụ thực tế |
| POST | `/api/v1/series/{id}/reactivate` | Mangaka, Admin | Khôi phục hoạt động từ Hiatus | Đối xứng với set-hiatus |

---

## 📦 MODULE: Submission (Vetting — MF1)

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| POST | `/api/v1/submissions/draft` | Mangaka | Tạo bản nháp submission |
| PUT | `/api/v1/submissions/{id}/manuscript` | Mangaka | Cập nhật URL bản thảo |
| PUT | `/api/v1/submissions/{id}/metadata` | Mangaka | Cập nhật metadata nháp |
| POST | `/api/v1/submissions/{id}/submit` | Mangaka | Nộp bản nháp lần đầu |
| POST | `/api/v1/submissions/{id}/resubmit` | Mangaka | Nộp lại sau khi sửa |
| GET | `/api/v1/submissions/my` | Mangaka | Danh sách submission của mình |
| GET | `/api/v1/submissions/queue` | EB, EIC, Admin | Hàng đợi duyệt (filter theo role) |
| POST | `/api/v1/submissions/{id}/vote` | EB | Bỏ phiếu tập thể |
| POST | `/api/v1/submissions/{id}/resolve-conflict` | EIC | Phân xử kết quả vote 1-1-1 |
| GET | `/api/v1/submissions/{id}` | All staff | Chi tiết submission |
| GET | `/api/v1/submissions/{id}/feedback-pins` | All staff | Các pin phản hồi hiện tại |
| GET | `/api/v1/submissions/{id}/feedback-pins/history` | All staff | Lịch sử toàn bộ pin phản hồi |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| DELETE | `/api/v1/submissions/{id}` | Mangaka | Xóa bản nháp (chỉ khi đang Draft) | Người dùng có thể hủy nháp |
| GET | `/api/v1/submissions/{id}/votes` | EB, EIC, Admin | Xem chi tiết từng phiếu bầu | Transparency, audit |
| GET | `/api/v1/board/reports` | EB, EIC | Báo cáo thống kê EB (tỉ lệ approve/reject, thời gian xử lý) | ReportsPage (Board) |

---

## 📦 MODULE: Chapter (MF2 — Production)

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| POST | `/api/v1/chapters` | Mangaka | Tạo chapter mới |
| GET | `/api/v1/chapters/series/{seriesId}` | Mangaka, TE, EB | Danh sách chapter theo series |
| GET | `/api/v1/chapters/{chapterId}` | Mangaka, TE | Chi tiết chapter + page tasks |
| POST | `/api/v1/chapters/{id}/pages` | Mangaka | Thêm trang vẽ (BasePage) |
| POST | `/api/v1/chapters/{id}/pages/activate` | Mangaka | Kích hoạt task trang và giao assistant |
| POST | `/api/v1/chapters/{id}/pages/region` | Mangaka | Vẽ vùng region cho task trang (SAM) |
| POST | `/api/v1/chapters/{id}/submit-for-qa` | Mangaka | Nộp chapter cho QA |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| PUT | `/api/v1/chapters/{id}` | Mangaka | Sửa thông tin chapter (title, cover, số trang) | Sửa sau khi tạo |
| DELETE | `/api/v1/chapters/{id}` | Mangaka | Xóa chapter (chỉ khi chưa có task nào) | Tạo nhầm cần xóa |
| GET | `/api/v1/chapters/{id}/pages` | Mangaka, TE | Danh sách trang và task của chapter | Màn hình quản lý trang |
| PATCH | `/api/v1/chapters/{id}/pages/{pageNum}/reassign` | Mangaka | Đổi assistant cho 1 trang | Thay người khi cần |
| GET | `/api/v1/chapters/my-queue` | TE | Chapter cần QA thuộc series TE phụ trách | ReviewQueuePage (Editor) |

---

## 📦 MODULE: Task (Assignment — MF2)

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| GET | `/api/v1/tasks/assigned` | Assistant | Danh sách task vẽ được giao |
| GET | `/api/v1/tasks/chapter/{chapterId}` | Mangaka | Tasks của 1 chapter |
| POST | `/api/v1/tasks/{pageTaskId}/layers` | Assistant | Nộp layer vẽ |
| POST | `/api/v1/tasks/{pageTaskId}/review` | Mangaka | Duyệt/từ chối layer của assistant |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| GET | `/api/v1/tasks/{pageTaskId}` | Mangaka, Assistant | Chi tiết 1 task và các layer đã nộp | Màn hình xem layer |
| GET | `/api/v1/tasks/{pageTaskId}/layers` | Mangaka, Assistant | Lịch sử các layer đã nộp của task | So sánh phiên bản |
| PATCH | `/api/v1/tasks/{pageTaskId}/deadline` | Mangaka | Cập nhật deadline của task | Quản lý tiến độ |
| GET | `/api/v1/assistant/tasks/income` | Assistant | Thống kê task hoàn thành + thu nhập ước tính | AssistantIncomePage |

---

## 📦 MODULE: Studio (Invitations — MF2)

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| POST | `/api/v1/studios/{seriesId}/invitations` | Mangaka | Mời assistant vào studio |
| GET | `/api/v1/studios/{seriesId}/invitations` | Mangaka | Danh sách lời mời đã gửi trong series |
| GET | `/api/v1/studios/invitations/pending` | Assistant | Lời mời đang chờ xử lý |
| POST | `/api/v1/studios/invitations/{id}/accept` | Assistant | Chấp nhận lời mời |
| POST | `/api/v1/studios/invitations/{id}/decline` | Assistant | Từ chối lời mời |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| GET | `/api/v1/studios/{seriesId}/members` | Mangaka, TE | Danh sách assistants đang trong studio | Quản lý nhân sự studio |
| DELETE | `/api/v1/studios/{seriesId}/members/{assistantId}` | Mangaka | Khai trừ assistant khỏi studio | Khi cần thay thế người |
| POST | `/api/v1/studios/invitations/{id}/cancel` | Mangaka | Hủy lời mời chưa được xử lý | Gửi nhầm cần thu hồi |

---

## 📦 MODULE: QA (MF3 — Quality Assurance)

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| POST | `/api/v1/qa/chapters/{id}/pins` | TE | Thêm bug pin vào chapter |
| POST | `/api/v1/qa/chapters/{id}/send-feedback` | TE | Gửi batch feedback (batch token) |
| GET | `/api/v1/qa/chapters/{id}/pins` | TE, Mangaka | Danh sách bug pins của chapter |
| POST | `/api/v1/qa/pins/{pinId}/resolve` | TE | Đánh dấu pin đã được xử lý |
| POST | `/api/v1/qa/chapters/{id}/approve` | TE | Duyệt chapter sau QA |
| GET | `/api/v1/qa/chapters/{id}/session` | TE, Mangaka | Thông tin QA session hiện tại |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| GET | `/api/v1/qa/chapters/{id}/history` | TE, Mangaka, Admin | Lịch sử toàn bộ các lần QA của chapter | Theo dõi quá trình cải thiện |
| POST | `/api/v1/qa/chapters/{id}/reopen` | TE | Mở lại QA session đã đóng nếu phát hiện thêm lỗi | Sửa lỗi bỏ sót |
| PATCH | `/api/v1/qa/pins/{pinId}` | TE | Chỉnh sửa nội dung pin đã tạo | Sửa comment nhầm |
| DELETE | `/api/v1/qa/pins/{pinId}` | TE | Xóa pin đã tạo nhầm | Xóa pin sai |

---

## 📦 MODULE: Publishing (MF3 — Publication)

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| POST | `/api/v1/publishing/schedule` | EB | Lên lịch phát hành chapter |
| POST | `/api/v1/publishing/publish` | EB, Admin | Phát hành chapter ngay lập tức |
| GET | `/api/v1/publishing/series/{seriesId}/history` | All | Lịch sử phát hành của series |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| GET | `/api/v1/publishing/chapters/my-queue` | TE | Danh sách chapter đã QA pass thuộc series TE quản lý | PublishingQueuePage (Editor) |
| GET | `/api/v1/publishing/schedule` | EB, Admin | Danh sách chương đã được lên lịch chưa phát hành | PublishingSchedulePage (Board) |
| PATCH | `/api/v1/publishing/schedule/{id}` | EB | Sửa lịch phát hành đã đặt | Thay đổi ngày |
| DELETE | `/api/v1/publishing/schedule/{id}` | EB | Hủy lịch phát hành | Lỡ lên lịch sai |
| GET | `/api/v1/publishing/chapters/{id}` | EB, Admin, TE | Chi tiết trạng thái phát hành 1 chapter | Kiểm tra trước khi publish |

---

## 📦 MODULE: Notifications

### Đã có ✅
| Method | Route | Role | Mô tả |
|---|---|---|---|
| GET | `/api/v1/notifications` | All | Danh sách thông báo (`?unreadOnly=true`) |
| PATCH | `/api/v1/notifications/{id}/read` | All | Đánh dấu đã đọc |
| PATCH | `/api/v1/notifications/read-all` | All | Đánh dấu tất cả đã đọc |

### Còn thiếu 🔨
| Method | Route | Role | Mô tả | Lý do cần |
|---|---|---|---|---|
| DELETE | `/api/v1/notifications/{id}` | All | Xóa 1 thông báo | UX: dọn dẹp thông báo cũ |
| DELETE | `/api/v1/notifications` | All | Xóa tất cả thông báo đã đọc | UX: clear all |
| GET | `/api/v1/notifications/unread-count` | All | Số lượng thông báo chưa đọc (badge trên icon) | Real-time badge count trên navbar |

---

## 📦 MODULE: Ranking (TOÀN BỘ CẦU VIẾT MỚI)

> Ranking module chỉ có Domain entities, chưa có Application layer, Infrastructure mapping, Controller.

### Cần viết mới 🔨
| Method | Route | Role | Mô tả |
|---|---|---|---|
| POST | `/api/v1/ranking/import` | Admin, EB | Import dữ liệu phiếu bầu thô cho 1 kỳ |
| POST | `/api/v1/ranking/compile` | Admin, EB | Chạy thuật toán xếp hạng và lưu snapshot |
| GET | `/api/v1/ranking/board` | Public | Bảng xếp hạng chính thức (`?period=26-2026`) |
| GET | `/api/v1/ranking/periods` | Public | Danh sách các kỳ đã có snapshot |
| GET | `/api/v1/ranking/import/{period}` | Admin, EB | Xem lại dữ liệu phiếu thô đã import của kỳ |
| DELETE | `/api/v1/ranking/import/{period}` | Admin | Xóa dữ liệu phiếu thô của kỳ (trước khi compile) |

---

## 🔧 SERVICES CÒN THIẾU (Infrastructure / Background Services)

| Service | Loại | Mô tả | Tại sao cần |
|---|---|---|---|
| **Email Service** | Infrastructure | Gửi email kích hoạt tài khoản, email reset mật khẩu | Hiện chỉ giả lập, cần tích hợp thật (SMTP/SendGrid) |
| **File Storage Service** | Infrastructure | Upload bản thảo, ảnh bìa, layer vẽ lên cloud (S3/Azure Blob) | Hiện FE truyền URL string thủ công |
| **Scheduled Publisher** | Background Job | Tự động phát hành chapter khi đến giờ lên lịch | `SchedulePublishCommand` lưu lịch nhưng không có job tự trigger |
| **SignalR Notification Hub** | Realtime | Đẩy thông báo real-time đến client | Đã có `INotificationService` nhưng cần đăng ký Hub và FE kết nối |
| **Audit Log Service** | Middleware/Interceptor | Ghi lại mọi hành động thay đổi dữ liệu (ai làm gì lúc mấy giờ) | Hệ thống sản xuất cần traceability |
| **Rate Limiter** | Middleware | Giới hạn số lần gọi API (chống spam login, spam vote) | Security cơ bản |
| **Health Check Endpoint** | Infrastructure | `GET /health` — kiểm tra trạng thái DB, services | DevOps / deployment monitoring |
| **Global Exception Handler** | Middleware | Chuẩn hóa format lỗi trả về cho tất cả unhandled exceptions | Hiện mỗi controller xử lý riêng, không nhất quán |

---

## 📊 TỔNG KẾT

| Hạng mục | Số lượng |
|---|---|
| ✅ API đã có | **41** |
| 🔨 API cần viết mới | **35** |
| 🔧 Service/Infrastructure còn thiếu | **8** |
| **Tổng cần bổ sung** | **43** |

### Ưu tiên thực hiện

| Mức | Nhóm | Lý do |
|---|---|---|
| 🔴 **P1 - Cần ngay** | `GET /users/me`, Notifications delete/count, `GET /series` (all), `GET /chapters/my-queue` (TE), SignalR Hub | Unblock FE tích hợp các trang đang placeholder |
| 🟡 **P2 - Quan trọng** | Cancellation flow (3 endpoints), Dashboard/Reports (Admin+Board), Publishing schedule CRUD, QA history | Hoàn thiện luồng nghiệp vụ chính |
| 🟢 **P3 - Nên có** | Forgot/Reset password, Avatar upload, Studio member management, Task deadline, Ranking full module | Hệ thống hoạt động hoàn chỉnh |
| ⚪ **P4 - Hạ tầng** | Scheduled Publisher job, File Storage thật, Audit Log, Rate Limiter, Health Check | Production-ready |
