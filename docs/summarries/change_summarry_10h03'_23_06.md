# Tổng hợp thay đổi Hệ thống Backend cho MF1 (Manga Feature 1)
**Thời gian:** 10:03, 23/06/2026  
**Chi nhánh:** `bao` (Monolith)

---

## 1. Tóm tắt các thay đổi nghiệp vụ & kỹ thuật

### #1 – Tự động chọn Tantou Editor (TE) khi EB duyệt
- **Vấn đề cũ:** EB phải chọn thủ công một `assignedEditorId` truyền lên qua HTTP request.
- **Thay đổi:**
  - Bỏ trường `AssignedEditorId` trong request model `/approve` ở `SubmissionsController` và `ApproveSubmissionCommand`.
  - Tối ưu hóa truy cập DB bằng phương thức mới `GetTantouEditorsLoadAsync` đếm trực tiếp trên Database bằng `COUNT + GROUP BY` thay vì load toàn bộ users vào bộ nhớ trong (tránh nghẽn OOM).
  - Khắc phục lỗi Race Condition bằng cách đưa logic truy vấn load và chọn TE vào **bên trong** Database Transaction (`strategy.ExecuteAsync`).
  - Phân bổ công việc công bằng nhờ cơ chế sắp xếp ưu tiên TE có load thấp nhất, sau đó ưu tiên TE được tạo sớm nhất (`OrderBy(x => x.Load).ThenBy(x => x.Editor.CreatedAt)`).

### #2 – Đồng nhất quy trình gán TE (Bỏ mâu thuẫn lúc tạo Mangaka)
- **Vấn đề cũ:** Bắt buộc nhập `ManagingTantouId` khi Admin tạo tài khoản Mangaka (`ProvisionAccount`), mâu thuẫn với việc gán TE sau khi Proposal được duyệt.
- **Thay đổi:**
  - Loại bỏ hoàn toàn `ManagingTantouId` khỏi request payload của `ProvisionAccount` (Controller, Command, và Validator). Tài khoản Mangaka mới được tạo sẽ mặc định có `ManagingTantouId = null`.
  - Giữ lại trường này trong `UpdateAccountCommand` để Admin vẫn có quyền thay đổi/điều chuyển TE thủ công sau này khi cần thiết.

### #3 – Bổ sung thông tin tác giả (Submitter) vào chi tiết bản thảo
- **Vấn đề cũ:** API `GetSubmissionDetail` chỉ trả về `submitterId` (GUID), khiến FE phải gọi thêm một API lấy thông tin người dùng gây nghẽn mạng.
- **Thay đổi:**
  - Inject `IUserRepository` vào `GetSubmissionDetailHandler`.
  - Trả về object nested `submitter` trong `SubmissionDetailDto` bao gồm: `{ userId, fullName, penName, personalEmail }`.

### #4 – API Notifications cho Mangaka
- **Vấn đề cũ:** BE đã có lưu và phát thông báo qua SignalR nhưng chưa có REST API để FE truy vấn lịch sử thông báo hoặc đánh dấu đã đọc.
- **Thay đổi:**
  - Thêm phương thức `GetAllByReceiverAsync` vào `INotificationRepository` và triển khai trong `PublishingRepositories.cs`.
  - Tạo mới query handler `GetMyNotificationsHandler.cs` hỗ trợ lấy thông báo theo ID của user đang đăng nhập (có thể lọc theo chưa đọc: `?unreadOnly=true`).
  - Tạo mới command handler `MarkNotificationReadHandler.cs` xử lý đánh dấu đã đọc đi kèm cơ chế kiểm tra quyền sở hữu và gọi lưu (`SaveChangesAsync`) tường minh.
  - Tạo mới `NotificationsController.cs` expose hai API: `GET /api/v1/notifications` và `PATCH /api/v1/notifications/{id}/read`.

### #5 – Sửa lỗi Deep-link của các thông báo
- **Vấn đề cũ:** URL trỏ đến `/workspace/...` không tồn tại ở Front-End.
- **Thay đổi:**
  - Cập nhật TargetUrl trong `NotificationService.cs` và `RequestRevisionHandler.cs` thành `/mangaka/series/{seriesId}` (khi được duyệt) và `/mangaka/submissions` (khi bị từ chối hoặc cần chỉnh sửa).

---

## 2. Chi tiết các file bị ảnh hưởng trong hệ thống

### A. Các file tạo mới (Untracked)

1. **`MangaERP.Publishing/Application/Queries/GetMyNotifications/GetMyNotificationsHandler.cs`**
   - Triển khai query `GetMyNotificationsQuery` và ánh xạ dữ liệu sang `NotificationDto`.
2. **`MangaERP.Publishing/Application/Commands/MarkNotificationRead/MarkNotificationReadHandler.cs`**
   - Xử lý command `MarkNotificationReadCommand`, kiểm tra `ReceiverId` chống đọc trộm và lưu trạng thái `IsRead = true` xuống DB.
3. **`MangaERP.Publishing/Presentation/Controllers/NotificationsController.cs`**
   - Định nghĩa controller, phân quyền sử dụng JWT `[Authorize]` và map endpoint với MediatR.

### B. Các file chỉnh sửa (Modified)

1. **`MangaERP.Submission/Application/Commands/ApproveSubmission/ApproveSubmissionHandler.cs`**
   - Bỏ AssignedEditorId trong Command & Validator; tích hợp logic tự động chọn TE sử dụng truy vấn đếm load dưới database bên trong Transaction.
2. **`MangaERP.Submission/Presentation/Controllers/SubmissionsController.cs`**
   - Loại bỏ body payload `ApproveRequest` và record tương ứng.
3. **`MangaERP.Identity/Application/Commands/ProvisionAccount/ProvisionAccountHandler.cs`**
   - Loại bỏ `ManagingTantouId` khi Admin tạo tài khoản.
4. **`MangaERP.Identity/Application/Commands/ProvisionAccount/ProvisionAccountValidator.cs`**
   - Bỏ rule validate bắt buộc điền `ManagingTantouId` cho Mangaka.
5. **`MangaERP.Identity/Presentation/Controllers/AdminController.cs`**
   - Sửa map request payload của Admin tạo tài khoản Mangaka.
6. **`MangaERP.Identity/Application/Ports/IIdentityPorts.cs`**
   - Khai báo thêm chữ ký hàm `GetTantouEditorsLoadAsync` trong `IUserRepository`.
7. **`MangaERP.Identity/Infrastructure/Repositories/IdentityRepositories.cs`**
   - Cài đặt hàm `GetTantouEditorsLoadAsync` dùng LINQ `GroupBy` và `Count`.
8. **`MangaERP.Submission/Application/Queries/GetSubmissionDetail/GetSubmissionDetailHandler.cs`**
   - Bổ sung query dữ liệu user tác giả đưa vào nested DTO của detail.
9. **`MangaERP.Publishing/Application/Ports/IPublishingPorts.cs`**
   - Thêm phương thức truy cập toàn bộ thông báo `GetAllByReceiverAsync` vào interface repository.
10. **`MangaERP.Shared.Infrastructure/Repositories/PublishingRepositories.cs`**
    - Cài đặt phương thức SQL lấy tất cả thông báo sắp xếp theo thời gian giảm dần.
11. **`MangaERP.Shared.Infrastructure/Services/NotificationService.cs`**
    - Sửa TargetUrl từ `/workspace` sang `/mangaka` khi gửi thông báo approved/rejected qua SignalR & DB.
12. **`MangaERP.Submission/Application/Commands/RequestRevision/RequestRevisionHandler.cs`**
    - Sửa TargetUrl trong Handler revision sang `/mangaka/submissions`.

---

## 3. Trạng thái biên dịch (Compilation Status)

Dự án đã được biên dịch thành công sau tất cả các thay đổi trên:
```bash
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
Các MediatR Handler đều được đăng ký tự động và sẵn sàng chạy thử nghiệm thực tế.
