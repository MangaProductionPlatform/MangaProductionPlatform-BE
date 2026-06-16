# Hướng Dẫn & Bộ JSON Test Từng API Hệ Thống Manga ERP

Tài liệu này tổng hợp toàn bộ các mẫu JSON Request và Response cho tất cả các API của 8 microservice, được thiết kế theo đúng quy trình nghiệp vụ để bạn chỉ việc copy-paste vào Swagger UI (`http://localhost:5010/swagger`) hoặc Postman để test.

---

## 1. IDENTITY SERVICE (Xác thực & Phân quyền)

### 1.1 Đăng ký tài khoản (Register)
*   **Method / Route**: `POST /api/v1/auth/register`
*   **JSON Request Body**:
```json
{
  "username": "hoang_mangaka",
  "email": "hoang.mangaka@example.com",
  "password": "Password123@",
  "fullName": "Nguyễn Huy Hoàng",
  "role": "Reader"
}
```
> [!NOTE]
> **Lưu ý về Enum `role`**: Nhờ có cấu hình StringEnumConverter mới, bạn có thể truyền kiểu chuỗi chữ (`"Reader"`, `"Mangaka"`, `"Assistant"`, `"TantouEditor"`, `"EditorialBoard"`) hoặc số nguyên tương ứng (`0` = Reader, `1` = Mangaka, `2` = Assistant, `3` = TantouEditor, `4` = EditorialBoard).

### 1.2 Đăng nhập (Login)
*   **Method / Route**: `POST /api/v1/auth/login`
*   **JSON Request Body**:
```json
{
  "email": "hoang.mangaka@example.com",
  "password": "Password123@"
}
```

---

## 2. SUBMISSION SERVICE (Đề xuất bản thảo - Quy trình 2 bước)

### 2.1 Gửi bản thảo đề xuất (Tài khoản Reader gửi)
*   **Method / Route**: `POST /api/v1/submissions`
*   **JSON Request Body**:
```json
{
  "submitterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Kiếm Sĩ Cuối Cùng",
  "description": "Hành trình bảo vệ vương quốc của kiếm sĩ ẩn dật.",
  "genre": "Action, Adventure, Historical",
  "coverImageUrl": "https://example.com/covers/kiemsicuoicung.jpg",
  "manuscriptUrl": "https://example.com/files/manuscript_chapter1.pdf"
}
```

### 2.2 Đề xuất duyệt bản thảo (Tài khoản TantouEditor duyệt bước 1)
*   **Method / Route**: `POST /api/v1/submissions/{submissionId}/recommend`
*   **JSON Request Body**:
```json
{
  "reviewerEditorId": "9c12ab91-23ef-4bb8-868c-4a37b38f8ab2",
  "feedbackMessage": "Cốt truyện và nét vẽ rất triển vọng. Đề xuất ban biên tập phê duyệt dự án này."
}
```

### 2.3 Phê duyệt chính thức (Tài khoản EditorialBoard duyệt bước 2)
*   **Method / Route**: `POST /api/v1/submissions/{submissionId}/approve?reviewerId={boardMemberId}`
*   **JSON Request Body**: *(Không cần body)*
*(Sau bước này, User sẽ tự động nâng cấp từ Role Reader lên Mangaka, đồng thời một Bộ truyện mới được tạo ở trạng thái Active)*

### 2.4 Yêu cầu sửa đổi (Nếu cần chỉnh sửa)
*   **Method / Route**: `POST /api/v1/submissions/{submissionId}/request-revision`
*   **JSON Request Body**:
```json
{
  "reviewerUserId": "9c12ab91-23ef-4bb8-868c-4a37b38f8ab2",
  "feedbackMessage": "Cần vẽ lại bìa truyện sáng sủa hơn và bổ sung phân cảnh chiến đấu ở trang 5."
}
```

### 2.5 Từ chối bản thảo
*   **Method / Route**: `POST /api/v1/submissions/{submissionId}/reject`
*   **JSON Request Body**:
```json
{
  "reviewerUserId": "9c12ab91-23ef-4bb8-868c-4a37b38f8ab2",
  "feedbackMessage": "Ý tưởng trùng lặp nhiều tác phẩm hiện hành, nội dung chưa đạt yêu cầu."
}
```

---

## 3. SERIES SERVICE (Quản lý bộ truyện)

### 3.1 Hủy/Ngưng phát hành bộ truyện (Tài khoản EditorialBoard thực hiện)
*   **Method / Route**: `POST /api/v1/series/{seriesId}/cancel`
*   **JSON Request Body**:
```json
{
  "reason": "Bộ truyện vi phạm bản quyền hình ảnh hoặc không đạt doanh thu tối thiểu."
}
```

---

## 4. CHAPTER SERVICE (Quản lý chương truyện & Trang vẽ)

### 4.1 Tạo chương mới cho bộ truyện (Tài khoản Mangaka thực hiện)
*   **Method / Route**: `POST /api/v1/chapters`
*   **JSON Request Body**:
```json
{
  "seriesId": "87eb6ad6-6dcc-41e9-bf9e-f887d51f3c3a",
  "title": "Chương 1: Lời nguyền cổ xưa",
  "chapterNumber": 1.0,
  "totalPages": 2,
  "assignedEditorId": "9c12ab91-23ef-4bb8-868c-4a37b38f8ab2"
}
```

### 4.2 Kích hoạt trang truyện và giao cho Assistant vẽ (Mangaka thực hiện)
*   **Method / Route**: `POST /api/v1/chapters/{chapterId}/pages/activate`
*   **JSON Request Body**:
```json
{
  "pageNumber": 1,
  "assignedAssistantId": "1a22ab91-23ef-4bb8-868c-4a37b38f8ab9"
}
```

### 4.3 Gửi chương lên Ban biên tập kiểm thử QA (Mangaka thực hiện)
*   **Method / Route**: `POST /api/v1/chapters/{chapterId}/submit-for-qa?mangakaId={mangakaUserId}`
*   **JSON Request Body**: *(Không cần body)*

---

## 5. TASK SERVICE (Quy trình sản xuất Layer vẽ)

### 5.1 Nộp bản vẽ layer mới (Tài khoản Assistant thực hiện)
*   **Method / Route**: `POST /api/v1/tasks/{pageTaskId}/layers`
*   **JSON Request Body**:
```json
{
  "assistantId": "1a22ab91-23ef-4bb8-868c-4a37b38f8ab9",
  "layerType": "LineArt",
  "fileUrlOriginal": "https://example.com/raw/c1_p1_lineart.psd",
  "fileUrlOptimized": "https://example.com/optimized/c1_p1_lineart.png"
}
```
> [!NOTE]
> **Lưu ý về Enum `layerType`**: Bạn có thể truyền chữ (`"LineArt"`, `"Background"`, `"Coloring"`, `"Text"`) hoặc số nguyên tương ứng (`0` = LineArt, `1` = Background, `2` = Coloring, `3` = Text).

### 5.2 Duyệt/Từ chối bản vẽ của Assistant (Tài khoản Mangaka thực hiện)
*   **Method / Route**: `POST /api/v1/tasks/{pageTaskId}/review`
*   **JSON Request Body**:
```json
{
  "reviewerMangakaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isAccepted": false,
  "rejectionNote": "Phông nền background trang này bị lệch tỉ lệ, cần điều chỉnh lại góc phối cảnh."
}
```
*(Nếu chấp nhận bản vẽ thì sửa `"isAccepted": true` và bỏ trường `"rejectionNote"`).*

---

## 6. QA SERVICE (Kiểm duyệt lỗi chất lượng trang truyện)

### 6.1 Ghim điểm báo lỗi trên trang vẽ (Tài khoản TantouEditor thực hiện)
*   **Method / Route**: `POST /api/v1/qa/chapters/{chapterId}/pins`
*   **JSON Request Body**:
```json
{
  "pageTaskId": "99fa5f64-5717-4562-b3fc-2c963f66afc4",
  "editorId": "9c12ab91-23ef-4bb8-868c-4a37b38f8ab2",
  "coordinateX": 25.50,
  "coordinateY": 80.00,
  "noteMessage": "Bị mất nét vẽ ở góc phải của khung thoại.",
  "issueType": "Visual",
  "batchToken": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```
> [!NOTE]
> **Lưu ý về Enum `issueType` (QA)**: Bạn có thể truyền chữ (`"Visual"`, `"Content"`, `"Text"`, `"Layout"`) hoặc số nguyên tương ứng (`0` = Visual, `1` = Content, `2` = Text, `3` = Layout).

### 6.2 Đóng/Xác nhận đã sửa lỗi xong (Tài khoản TantouEditor thực hiện)
*   **Method / Route**: `POST /api/v1/qa/pins/{pinId}/resolve`
*   **JSON Request Body**: *(Không cần body)*

### 6.3 Phê duyệt chương đạt tiêu chuẩn chất lượng (Tài khoản TantouEditor thực hiện)
*   **Method / Route**: `POST /api/v1/qa/chapters/{chapterId}/approve?editorId={editorUserId}`
*   **JSON Request Body**: *(Không cần body)*

---

## 7. PUBLISHING SERVICE (Lịch trình & Phát hành)

### 7.1 Lên lịch đăng truyện (Tài khoản EditorialBoard thực hiện)
*   **Method / Route**: `POST /api/v1/publishing/schedule`
*   **JSON Request Body**:
```json
{
  "chapterId": "23fa5f64-5717-4562-b3fc-2c963f66afb2",
  "seriesId": "87eb6ad6-6dcc-41e9-bf9e-f887d51f3c3a",
  "issueType": "Weekly",
  "scheduledPublishAt": "2026-06-20T08:00:00Z"
}
```
> [!NOTE]
> **Lưu ý về Enum `issueType` (Publishing)**: Bạn có thể truyền chữ (`"Weekly"`, `"BiWeekly"`, `"Monthly"`, `"Special"`) hoặc số nguyên tương ứng (`0` = Weekly, `1` = BiWeekly, `2` = Monthly, `3` = Special).

### 7.2 Phát hành chương ngay lập tức (Tài khoản EditorialBoard hoặc Hệ thống tự động)
*   **Method / Route**: `POST /api/v1/publishing/publish`
*   **JSON Request Body**:
```json
{
  "chapterId": "23fa5f64-5717-4562-b3fc-2c963f66afb2"
}
```

---

## 8. RANKING SERVICE (Bình chọn & Xếp hạng)

### 8.1 Nhập dữ liệu bình chọn (Tài khoản EditorialBoard nhập định kỳ)
*   **Method / Route**: `POST /api/v1/ranking/import`
*   **JSON Request Body**:
```json
{
  "seriesId": "87eb6ad6-6dcc-41e9-bf9e-f887d51f3c3a",
  "votesCount": 1550,
  "viewsCount": 25000,
  "weekNumber": 24,
  "year": 2026
}
```
