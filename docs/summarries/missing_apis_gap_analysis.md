# Phân Tích Khoảng Trống API (Gap Analysis) & Bản Đồ Phân Quyền Vai Trò
> **Mục tiêu:** Liệt kê toàn bộ các API còn thiếu trên Backend để vận hành các trang hỗ trợ, thống kê, giám sát quy trình, và làm rõ luồng nghiệp vụ duyệt hủy truyện (Cancellation Review) giữa Admin và Editor-in-Chief (EIC).

---

## 1. 🔍 Làm rõ Luồng Nghiệp Vụ: Cancellation Review

> [!IMPORTANT]
> **Định vị vai trò đúng:** Quyết định dừng/hủy một bộ truyện (Series Cancellation) là quyết định chuyên môn cao thuộc về **Ban Biên Tập (Editorial Board)** và **Tổng Biên Tập (Editor-in-Chief)**, không thuộc thẩm quyền của Admin. Admin chỉ đảm nhận việc vận hành hệ thống kỹ thuật và quản trị tài khoản.

*   **Vị trí trang trên FE:** `src/pages/board/CancellationReviewPage.tsx` (Thuộc phân hệ của Editorial Board).
*   **Quy trình nghiệp vụ đề xuất:**
    1. Mangaka gửi yêu cầu tạm dừng/hủy bộ truyện (Request Cancel) kèm lý do.
    2. Bộ truyện chuyển sang trạng thái chờ duyệt hủy. Yêu cầu xuất hiện trong danh sách hàng đợi duyệt của EB/EIC.
    3. EIC hoặc Editorial Board xem xét lý do và đưa ra quyết định phê duyệt (Approve) hoặc bác bỏ (Reject).
*   **Các API Backend đang thiếu:**
    *   `GET /api/v1/series/cancellation-queue` $\rightarrow$ Lấy danh sách các tác phẩm đang gửi yêu cầu hủy (Chỉ cho EB, EIC, Admin).
    *   `POST /api/v1/series/{id}/approve-cancellation` $\rightarrow$ EIC/EB đồng ý hủy bộ truyện (Chuyển trạng thái `MangaSeries.Status` thành `Cancelled`).
    *   `POST /api/v1/series/{id}/reject-cancellation` $\rightarrow$ Bác bỏ yêu cầu hủy, khôi phục trạng thái hoạt động bình thường.

---

## 2. 📊 API hỗ trợ Hệ thống & Giám sát (Admin & Board)

Dưới đây là các API cần bổ sung trên Backend để phục vụ cho các trang báo cáo, thống kê và giám sát quy trình:

| Vai trò | Trang Frontend bị ảnh hưởng | API Backend đề xuất | Mục đích & Dữ liệu trả về | Trạng thái tích hợp |
| :--- | :--- | :--- | :--- | :--- |
| **Admin** | `AdminDashboardPage.tsx` | `GET /api/v1/admin/dashboard` | Trả về các chỉ số tổng quan hệ thống: Tổng người dùng (Active/Pending), tổng số Series, tổng số Submission đang vetting, tổng số Chapter đang vẽ. | 🔨 Cần viết mới |
| **Admin** | `AdminRolesPage.tsx` | `GET /api/v1/admin/roles` | Trả về danh sách tĩnh các vai trò hiện có trong hệ thống và mô tả nhiệm vụ đi kèm (Hệ thống dùng RBAC tĩnh, không cần CRUD Role). | 🔨 Cần viết mới |
| **Admin** | `AdminWorkflowMonitoringPage.tsx` | `GET /api/v1/admin/workflow-stats` | Thống kê trạng thái vận hành của các luồng công việc để phát hiện tắc nghẽn (Ví dụ: Số chapter đang QA, số chapter đang vẽ, số submission bị tranh chấp). | 🔨 Cần viết mới |
| **Admin** | `AdminReportsAnalyticsPage.tsx` | `GET /api/v1/admin/reports` | Biểu đồ lịch sử tăng trưởng của Series và các Chapter được xuất bản theo tháng. | 🔨 Cần viết mới |
| **Board** | `ReportsPage.tsx` | `GET /api/v1/board/reports` | Thống kê hiệu suất làm việc nội bộ của Ban biên tập (Tỉ lệ duyệt/từ chối submission, thời gian xử lý trung bình). | 🔨 Cần viết mới |
| **Board** | `RankingAnalyticsPage.tsx` | Luồng Ranking (A hoặc B) | Dành cho việc nhập dữ liệu vote thô (`/import`) và biên soạn bảng xếp hạng (`/compile`). | 🔨 Cần viết mới |

---

## 3. 🗺️ Bản Đồ Trạng Thái API cho các Vai Trò Khác

### A. Tantou Editor (TE - Biên tập viên phụ trách)
*   **`PublishingQueuePage.tsx` (Hàng đợi phát hành):**
    *   *Tình trạng:* 🔨 Thiếu API.
    *   *Giải pháp:* Backend cần bổ sung endpoint `GET /api/v1/publishing/chapters/my-queue`. API này sẽ tự động đọc JWT token để lấy ID của Tantou Editor đang đăng nhập và trả về danh sách các Chapter đã QA pass thuộc các Series do editor này trực tiếp phụ trách.
*   **`RankingReportsPage.tsx` (Báo cáo xếp hạng):**
    *   *Giải pháp:* ✅ Đã có hướng xử lý. Không cần viết API mới, FE sử dụng chung API bảng xếp hạng công khai `GET /api/v1/ranking/board`.
*   **`EditorWorkspacePage.tsx` & `SeriesMonitoringPage.tsx`:**
    *   *Giải pháp:* ✅ Đã có sẵn. Backend đã có `GET /api/v1/series` (tự động filter theo `ManagingTantouId` của token) và `GET /api/v1/chapters?seriesId=...`. FE chỉ cần kết nối và hiển thị dữ liệu.

### B. Assistant (Trợ lý vẽ)
*   **`AssistantIncomePage.tsx` (Báo cáo thu nhập):**
    *   *Tình trạng:* 🔨 Thiếu API (do dự án ERP sản xuất chưa phát triển phân hệ Tài chính/Thanh toán).
    *   *Giải pháp giả lập:* Backend cung cấp một API thống kê nhiệm vụ hoàn thành nhân với đơn giá giả định: `GET /api/v1/assistant/tasks/income` $\rightarrow$ Trả về: `{ totalFinishedTasks: X, estimatedIncome: X * đơn_giá_giả_định }`.
*   **`AssistantSubmissionsPage.tsx` & `AssistantChaptersPage.tsx`:**
    *   *Giải pháp:* ✅ Đã có sẵn. Sử dụng các API liên quan đến nhiệm vụ vẽ trong module `Task` (`GET /api/v1/tasks/assigned` để lấy các layer vẽ được giao và `POST /api/v1/tasks/{id}/submit-layer` để nộp bài vẽ).
*   **`AssistantNotificationsPage.tsx`:**
    *   *Giải pháp:* ✅ Đã có sẵn. Sử dụng chung API thông báo hệ thống `GET /api/v1/notifications`.

### C. Mangaka (Tác giả)
*   Tất cả các giao diện cốt lõi của tác giả bao gồm: quản lý trợ lý vẽ, giao nhiệm vụ vẽ theo từng trang (`TaskAssignmentPage.tsx`), và duyệt hình vẽ của trợ lý (`LayerReviewPage.tsx`) **đều đã có đầy đủ API** hỗ trợ tại module `Task` và `Studio`.

### D. Public Reader (Độc giả vãng lai)
*   Các trang của độc giả như: `DiscoverPage`, `TrendingPage`, `GenresPage`, `CreatorPage` nằm ngoài phạm vi cốt lõi của hệ thống ERP sản xuất nội bộ này.
*   *Hướng xử lý:* Frontend có thể dùng Mock data hoặc gọi API danh sách series hoạt động (`GET /api/v1/series`) để hiển thị thông tin cơ bản.
