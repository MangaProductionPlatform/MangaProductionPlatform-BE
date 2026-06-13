# MangaERP — Ultra-Short API Matrix (FE Cheatsheet)

> **Ghi chú trạng thái:**
> * ✅ **Đã có sẵn (Ready to call)**
> * 🔲 **Chưa có (Đang phát triển)**

---

## 1. Auth & Admin

| Method | Endpoint (Role & URL) | Payload (Request Parameters / Body keys) | Mô tả |
|------------------------------------------|-----------------------|------|------------|
| POST | ✅ `[Public] /api/v1/auth/login` | `{ email, password }` | Đăng nhập hệ thống |
| POST | ✅ `[Public] /api/v1/auth/activate` | `{ token, newPassword }` | Kích hoạt tài khoản lần đầu qua email |
| POST | ✅ `[Admin] /api/v1/admin/accounts/provision` | `{ fullName, personalEmail, role, phoneNumber, managingTantouId }` | Cấp tài khoản mới cho nhân sự/tác giả |
| GET  | ✅ `[Admin] /api/v1/admin/accounts` | `?roleFilter, ?statusFilter` | Xem danh sách tài khoản  |
| GET | ✅ `[Admin] /api/v1/admin/accounts/{userId}` | *(Trống)* | Xem chi tiết tài khoản |
| PUT | ✅ `[All Roles] /api/v1/users/profile` | `{ penName, drawingSoftwares, bankAccountNumber }` | Cập nhật hồ sơ cá nhân |

---

## 2. Submission & Series (Workflow MF1)

| Method | Endpoint (Role & URL) | Payload (Request Parameters / Body keys) | Mô tả |
|---|---|---|---|
| POST | ✅ `[Mangaka] /api/v1/submissions/draft` | `{ title, description, genre, coverImageUrl, manuscriptUrl }` | Tạo bản nháp đề xuất bản thảo mới |
| PUT | ✅ `[Mangaka] /api/v1/submissions/{id}/metadata` | `{ title, description, genre, coverImageUrl }` | Cập nhật metadata bản nháp |
| PUT | ✅ `[Mangaka] /api/v1/submissions/{id}/manuscript` | `{ manuscriptUrl }` | Cập nhật link file bản thảo |
| POST | ✅ `[Mangaka] /api/v1/submissions/{id}/submit` | *(Trống)* | Nộp đề xuất bản thảo lần đầu |
| POST | ✅ `[Mangaka] /api/v1/submissions/{id}/resubmit` | *(Trống)* | Nộp lại bản thảo sau khi sửa đổi |
| GET | ✅ `[Mangaka] /api/v1/submissions/my` | `?statusFilter` | Xem danh sách đề xuất của tác giả |
| GET | ✅ `[TantouEditor, EditorialBoard, Admin] /api/v1/submissions/queue` | *(Trống)* | Xem hàng đợi duyệt theo vai trò (TE thấy Pending_TE_Review, EB thấy Pending_EB_Review) |
| GET | ✅ `[Mangaka, TantouEditor, EditorialBoard, Admin] /api/v1/submissions/{id}` | *(Trống)* | Xem chi tiết đề xuất bản thảo |
| POST | ✅ `[TantouEditor] /api/v1/submissions/{id}/start-review` | *(Trống)* | TE nhận kiểm duyệt bản thảo |
| POST | ✅ `[TantouEditor] /api/v1/submissions/{id}/recommend` | `{ recommendationMessage }` | TE đề xuất bản thảo lên EB duyệt |
| POST | ✅ `[TantouEditor] /api/v1/submissions/{id}/te-request-revision` | `{ reason }` | TE yêu cầu tác giả sửa lại bản thảo |
| POST | ✅ `[EditorialBoard] /api/v1/submissions/{id}/eb-request-revision` | `{ reason }` | EB yêu cầu tác giả sửa lại bản thảo |
| POST | ✅ `[TantouEditor] /api/v1/submissions/{id}/te-reject` | `{ reason }` | TE từ chối bản thảo |
| POST | ✅ `[EditorialBoard] /api/v1/submissions/{id}/eb-reject` | `{ reason }` | EB từ chối bản thảo |
| POST | ✅ `[EditorialBoard] /api/v1/submissions/{id}/approve` | *(Trống)* | EB phê duyệt bản thảo (tạo Series mới) |
| GET | ✅ `[Mangaka] /api/v1/series/my` | *(Trống)* | Xem danh sách bộ truyện của Mangaka |
| GET | ✅ `[Mangaka, TantouEditor, EditorialBoard, Admin] /api/v1/series/{id}` | *(Trống)* | Xem chi tiết bộ truyện |

---

## 3. Studio (Luồng Assistant)

| Method | Endpoint (Role & URL) | Payload (Request Parameters / Body keys) | Mô tả |
|---|---|---|---|
| POST | 🔲 `[Mangaka] /api/v1/studios/{studioId}/invitations` | `{ assistantEmail, message }` | Mangaka mời Assistant vào studio |
| GET | 🔲 `[Assistant] /api/v1/studios/invitations/pending` | *(Trống)* | Assistant xem danh sách lời mời đang chờ |
| POST | 🔲 `[Assistant] /api/v1/studios/invitations/{invitationId}/accept` | *(Trống)* | Assistant chấp nhận lời mời vào studio |
| POST | 🔲 `[Assistant] /api/v1/studios/invitations/{invitationId}/decline` | *(Trống)* | Assistant từ chối lời mời vào studio |
| GET | 🔲 `[Mangaka] /api/v1/studios/{studioId}/members` | *(Trống)* | Mangaka xem danh sách thành viên studio |
| DELETE | 🔲 `[Mangaka] /api/v1/studios/{studioId}/members/{assistantId}` | *(Trống)* | Mangaka loại Assistant khỏi studio |

---

## 4. Pipeline (Chapter, Tasks, QA & Publishing - Workflow MF2 & MF3)

| Method | Endpoint (Role & URL) | Payload (Request Parameters / Body keys) | Mô tả |
|---|---|---|---|
| POST | 🔲 `[Mangaka] /api/v1/chapters` | `{ seriesId, title, chapterNumber, totalPages, assignedEditorId }` | Mangaka tạo chương truyện mới |
| POST | 🔲 `[Mangaka] /api/v1/chapters/{chapterId}/pages/activate` | `{ pageNumber, assignedAssistantId }` | Kích hoạt trang truyện và giao cho Assistant |
| GET | 🔲 `[Mangaka, TantouEditor, EditorialBoard] /api/v1/chapters/series/{seriesId}` | *(Trống)* | Xem danh sách chương của bộ truyện |
| POST | 🔲 `[Assistant] /api/v1/tasks/{pageTaskId}/layers` | `{ layerType, fileUrlOriginal, fileUrlOptimized }` | Assistant upload bản vẽ layer của trang |
| POST | 🔲 `[Mangaka] /api/v1/tasks/{pageTaskId}/review` | `{ isAccepted, rejectionNote }` | Mangaka duyệt hoặc yêu cầu sửa lại trang |
| POST | 🔲 `[Mangaka] /api/v1/chapters/{chapterId}/submit-for-qa` | *(Trống)* | Mangaka nộp chương truyện lên hàng đợi QA |
| POST | 🔲 `[TantouEditor] /api/v1/qa/chapters/{chapterId}/pins` | `{ pageTaskId, coordinateX, coordinateY, noteMessage, issueType, batchToken }` | TE ghim điểm lỗi trên trang truyện |
| GET | 🔲 `[TantouEditor, Mangaka] /api/v1/qa/chapters/{chapterId}/pins` | *(Trống)* | Xem tất cả ghim lỗi của chương truyện |
| POST | 🔲 `[TantouEditor] /api/v1/qa/pins/{pinId}/resolve` | *(Trống)* | TE đánh dấu đã sửa xong lỗi ghim |
| POST | 🔲 `[TantouEditor] /api/v1/qa/chapters/{chapterId}/approve` | *(Trống)* | TE phê duyệt chương truyện đạt chuẩn QA |
| POST | 🔲 `[EditorialBoard] /api/v1/publishing/schedule` | `{ chapterId, seriesId, issueType, scheduledPublishAt }` | EB lên lịch phát hành chương truyện |
| POST | 🔲 `[EditorialBoard] /api/v1/publishing/publish` | `{ chapterId }` | EB xuất bản chương truyện ngay lập tức |

---

## 5. Ranking

| Method | Endpoint (Role & URL) | Payload (Request Parameters / Body keys) | Mô tả |
|---|---|---|---|
| POST | 🔲 `[EditorialBoard] /api/v1/ranking/import` | `{ seriesId, votesCount, viewsCount, weekNumber, year }` | EB nhập dữ liệu bình chọn định kỳ |
| GET | 🔲 `[Public] /api/v1/ranking/board` | `?votePeriod` | Xem bảng xếp hạng bộ truyện (Public) |
