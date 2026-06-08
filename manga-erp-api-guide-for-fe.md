# MangaERP Frontend Integration Guide

This document is designed for the Frontend Developer and their AI Coding Agent. It outlines the architecture, authentication, and step-by-step API integration patterns needed to implement the three main business workflows (MF1, MF2, MF3) of the MangaERP platform.

---

## 1. Environment & Architecture
* **API Gateway (YARP)**: `http://localhost:5010` (All microservice APIs are consolidated here).
* **SignalR WebSocket Hub**: `http://localhost:5009/notificationHub` (For real-time workflow notifications).
* **Aggregated Swagger UI**: `http://localhost:5010/swagger`

---

## 2. Actors & Role-Based Access Control (RBAC)
The backend enforces role validations using JWT claims. Ensure your application manages the user's current role to display the correct screens and attach appropriate authorization headers.

| Role | Description |
|---|---|
| `Reader` | The default role after registration. Can read published chapters, draft & submit series proposals. |
| `Mangaka` | Elevated automatically from Reader when a series proposal is approved. Owns the Studio Workspace. |
| `Assistant` | Invited by a Mangaka. Uploads artwork layers for assigned pages. |
| `TantouEditor` | Reviews pages, pins bug locations, and approves/rejects chapters for QA. |
| `EditorialBoard` | Approves series submissions, schedules releases, cancels series, and uploads vote data. |

---

## 3. Core Headers & Authentication Flow
Every request to a protected endpoint must include:
* **Header**: `Authorization: Bearer <JWT_ACCESS_TOKEN>`

### 3.1 Register Account
* **Endpoint**: `POST http://localhost:5010/api/v1/auth/register`
* **Request Body**:
  ```json
  {
    "username": "tuan_mangaka",
    "email": "tuan.mangaka@example.com",
    "password": "Password123@",
    "fullName": "Nguyễn Văn Tuấn",
    "role": "Mangaka" // Options: "Reader", "Mangaka", "Assistant", "TantouEditor", "EditorialBoard"
  }
  ```
* **Response (201 Created)**:
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "tuan_mangaka",
    "email": "tuan.mangaka@example.com",
    "role": "Mangaka"
  }
  ```

### 3.2 Login (Authenticate)
* **Endpoint**: `POST http://localhost:5010/api/v1/auth/login`
* **Request Body**:
  ```json
  {
    "email": "tuan.mangaka@example.com",
    "password": "Password123@"
  }
  ```
* **Response (200 OK)**:
  ```json
  {
    "token": "eyJhbGciOi...",
    "refreshToken": "d82bd5e...",
    "role": "Mangaka",
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
  ```

---

## 4. MF1: Series Submission & Vetting Workflow (Two-Stage Vetting)

```mermaid
graph TD
    Reader[Reader registers & logs in] --> CreateSub[POST /api/v1/submissions]
    CreateSub --> Rec[POST /api/v1/submissions/{id}/recommend by Editor]
    CreateSub --> Reject[POST /api/v1/submissions/{id}/reject by Editor/Board]
    CreateSub --> Rev[POST /api/v1/submissions/{id}/request-revision by Editor/Board]
    Rec --> Approve[POST /api/v1/submissions/{id}/approve by Board]
    Approve --> Elevate[System Elevates User to Mangaka & Creates Series]
```

### Step 1: Reader Submits a Series Proposal
* **Endpoint**: `POST http://localhost:5010/api/v1/submissions`
* **Headers**: `Authorization: Bearer <token>` (User must be `Reader` or `Mangaka`)
* **Request Body**:
  ```json
  {
    "submitterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Hành Trình Kỳ Thú",
    "description": "Câu chuyện phiêu lưu của cậu bé Tèo tại thế giới song song.",
    "genre": "Adventure, Fantasy",
    "coverImageUrl": "https://example.com/images/cover.jpg",
    "manuscriptUrl": "https://example.com/files/manuscript.pdf"
  }
  ```
* **Response (201 Created)**:
  ```json
  {
    "submissionId": "7d9ab1a0-56ef-4bb8-868c-4a37b38f8ab6"
  }
  ```

### Step 2: Tantou Editor Reviews & Recommends to Board
* **Endpoint**: `POST http://localhost:5010/api/v1/submissions/{submissionId}/recommend`
* **Headers**: `Authorization: Bearer <token>` (User must be `TantouEditor`)
* **Request Body**:
  ```json
  {
    "reviewerEditorId": "9c12ab91-23ef-4bb8-868c-4a37b38f8ab2",
    "feedbackMessage": "Cốt truyện hấp dẫn, tạo hình nhân vật tốt. Đề xuất duyệt bản thảo này."
  }
  ```
* **Response**: `204 No Content`

### Step 3: Editorial Board Approves (Elevates Role to Mangaka)
* **Endpoint**: `POST http://localhost:5010/api/v1/submissions/{submissionId}/approve?reviewerId={boardMemberId}`
* **Headers**: `Authorization: Bearer <token>` (User must be `EditorialBoard`)
* **Response**: `204 No Content`
*(Side-effect: User is now a `Mangaka` and a new series is created in `Active` status. The frontend should prompt the user to re-login to update their token claims).*

### Other MF1 Handlers:
* **Reject Submission**: `POST http://localhost:5010/api/v1/submissions/{id}/reject`
  * Body: `{"reviewerUserId": "guid", "feedbackMessage": "string"}`
* **Request Revision**: `POST http://localhost:5010/api/v1/submissions/{id}/request-revision`
  * Body: `{"reviewerUserId": "guid", "feedbackMessage": "string"}`

---

## 5. MF2: Manga Production Workflow (Studio Pipeline)

```mermaid
graph TD
    Mangaka[Mangaka creates Chapter] --> CreateChapter[POST /api/v1/chapters]
    CreateChapter --> ActivatePage[POST /api/v1/chapters/{chapterId}/pages/activate]
    ActivatePage --> AssistantUpload[POST /api/v1/tasks/{pageTaskId}/layers]
    AssistantUpload --> Review[POST /api/v1/tasks/{pageTaskId}/review]
    Review -- Rejected --> AssistantUpload
    Review -- Approved --> SubmitQA[POST /api/v1/chapters/{id}/submit-for-qa]
```

### Step 1: Mangaka Creates a Chapter
* **Endpoint**: `POST http://localhost:5010/api/v1/chapters`
* **Headers**: `Authorization: Bearer <token>` (User must be `Mangaka`)
* **Request Body**:
  ```json
  {
    "seriesId": "87eb6ad6-6dcc-41e9-bf9e-f887d51f3c3a",
    "title": "Chương 1: Khởi đầu mới",
    "chapterNumber": 1.0,
    "totalPages": 20,
    "assignedEditorId": "9c12ab91-23ef-4bb8-868c-4a37b38f8ab2"
  }
  ```
* **Response (201 Created)**:
  ```json
  {
    "chapterId": "23fa5f64-5717-4562-b3fc-2c963f66afb2",
    "title": "Chương 1: Khởi đầu mới",
    "chapterNumber": 1.0,
    "totalPages": 20
  }
  ```

### Step 2: Mangaka Activates a Page and Assigns an Assistant
* **Endpoint**: `POST http://localhost:5010/api/v1/chapters/{chapterId}/pages/activate`
* **Headers**: `Authorization: Bearer <token>` (User must be `Mangaka`)
* **Request Body**:
  ```json
  {
    "pageNumber": 1,
    "assignedAssistantId": "1a22ab91-23ef-4bb8-868c-4a37b38f8ab9"
  }
  ```
* **Response (200 OK)**:
  ```json
  {
    "pageTaskId": "99fa5f64-5717-4562-b3fc-2c963f66afc4",
    "pageNumber": 1,
    "status": "Incomplete"
  }
  ```

### Step 3: Assistant Uploads an Artwork Layer
* **Endpoint**: `POST http://localhost:5010/api/v1/tasks/{pageTaskId}/layers`
* **Headers**: `Authorization: Bearer <token>` (User must be `Assistant`)
* **Request Body**:
  ```json
  {
    "assistantId": "1a22ab91-23ef-4bb8-868c-4a37b38f8ab9",
    "layerType": "LineArt", // Enum values: "LineArt", "Background", "Coloring", "Text"
    "fileUrlOriginal": "https://example.com/raw/page1_lineart.psd",
    "fileUrlOptimized": "https://example.com/optimized/page1_lineart.png"
  }
  ```
* **Response (200 OK)**:
  ```json
  {
    "layerId": "b12fa564-5717-4562-b3fc-2c963f66af11",
    "version": 1 // Versions increment automatically (MF7)
  }
  ```

### Step 4: Mangaka Reviews the Layer
* **Endpoint**: `POST http://localhost:5010/api/v1/tasks/{pageTaskId}/review`
* **Headers**: `Authorization: Bearer <token>` (User must be `Mangaka`)
* **Request Body**:
  ```json
  {
    "reviewerMangakaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "isAccepted": false,
    "rejectionNote": "Nét line ở góc dưới bên trái bị đứt quãng, cần vẽ lại liền mạch." // Required if isAccepted = false
  }
  ```
* **Response**: `204 No Content`

### Step 5: Mangaka Submits Completed Chapter for Editorial QA
* **Endpoint**: `POST http://localhost:5010/api/v1/chapters/{chapterId}/submit-for-qa?mangakaId={mangakaUserId}`
* **Headers**: `Authorization: Bearer <token>` (User must be `Mangaka`)
* **Response**: `204 No Content`

---

## 6. MF3: Visual QA & Publishing Flow

```mermaid
graph TD
    Editor[Editor checks Chapter] --> AddPin[POST /api/v1/qa/chapters/{chapterId}/pins]
    AddPin --> GetPins[GET /api/v1/qa/chapters/{chapterId}/pins]
    GetPins --> ApproveChapter[POST /api/v1/qa/chapters/{chapterId}/approve]
    ApproveChapter --> SchedulePublish[POST /api/v1/publishing/schedule by Board]
    SchedulePublish --> Publish[POST /api/v1/publishing/publish by system/Board]
```

### Step 1: Tantou Editor Pins Bug/Issue on Page Composite
* **Endpoint**: `POST http://localhost:5010/api/v1/qa/chapters/{chapterId}/pins`
* **Headers**: `Authorization: Bearer <token>` (User must be `TantouEditor`)
* **Request Body**:
  ```json
  {
    "pageTaskId": "99fa5f64-5717-4562-b3fc-2c963f66afc4",
    "editorId": "9c12ab91-23ef-4bb8-868c-4a37b38f8ab2",
    "coordinateX": 45.50, // Percent coordinate (0.00 - 100.00)
    "coordinateY": 62.30, // Percent coordinate (0.00 - 100.00)
    "noteMessage": "Lỗi chính tả từ 'xuất sắc' thành 'suất sắc'.",
    "issueType": "Text", // Enums: "Visual", "Content", "Text", "Layout"
    "batchToken": "5fa85f64-5717-4562-b3fc-2c963f66afa2" // Client generated UUID to group pins in one review submit
  }
  ```
* **Response (200 OK)**:
  ```json
  {
    "id": "e22ab91-23ef-4bb8-868c-4a37b38f8ab9",
    "pageTaskId": "99fa5f64-5717-4562-b3fc-2c963f66afc4",
    "coordinateX": 45.50,
    "coordinateY": 62.30,
    "noteMessage": "Lỗi chính tả từ 'xuất sắc' thành 'suất sắc'."
  }
  ```

### Step 2: Get All Bug Pins for a Chapter (Mangaka & Editor Views)
* **Endpoint**: `GET http://localhost:5010/api/v1/qa/chapters/{chapterId}/pins`
* **Headers**: `Authorization: Bearer <token>` (User must be `TantouEditor` or `Mangaka`)
* **Response (200 OK)**:
  ```json
  [
    {
      "id": "e22ab91-23ef-4bb8-868c-4a37b38f8ab9",
      "pageTaskId": "99fa5f64-5717-4562-b3fc-2c963f66afc4",
      "coordinateX": 45.50,
      "coordinateY": 62.30,
      "noteMessage": "Lỗi chính tả từ 'xuất sắc' thành 'suất sắc'.",
      "issueType": "Text",
      "isResolved": false
    }
  ]
  ```

### Step 3: Editor Approves Chapter
* **Endpoint**: `POST http://localhost:5010/api/v1/qa/chapters/{chapterId}/approve?editorId={editorUserId}`
* **Headers**: `Authorization: Bearer <token>` (User must be `TantouEditor`)
* **Response**: `204 No Content`

### Step 4: Editorial Board Schedules Publication
* **Endpoint**: `POST http://localhost:5010/api/v1/publishing/schedule`
* **Headers**: `Authorization: Bearer <token>` (User must be `EditorialBoard`)
* **Request Body**:
  ```json
  {
    "chapterId": "23fa5f64-5717-4562-b3fc-2c963f66afb2",
    "seriesId": "87eb6ad6-6dcc-41e9-bf9e-f887d51f3c3a",
    "issueType": "Weekly",
    "scheduledPublishAt": "2026-06-15T08:00:00Z"
  }
  ```
* **Response**: `204 No Content`

### Step 5: Publishing the Chapter (Manual or Background job)
* **Endpoint**: `POST http://localhost:5010/api/v1/publishing/publish`
* **Headers**: `Authorization: Bearer <token>` (User must be `EditorialBoard`)
* **Request Body**:
  ```json
  {
    "chapterId": "23fa5f64-5717-4562-b3fc-2c963f66afb2",
    "seriesId": "87eb6ad6-6dcc-41e9-bf9e-f887d51f3c3a",
    "productionFileUrl": "https://example.com/published/chapter1_final.pdf"
  }
  ```
* **Response**: `204 No Content`

---

## 7. Supporting Queries

### 7.1 View Series Catalog (Public / Guest accessible)
* **Endpoint**: `GET http://localhost:5010/api/v1/series`
* **Response (200 OK)**:
  ```json
  [
    {
      "id": "87eb6ad6-6dcc-41e9-bf9e-f887d51f3c3a",
      "title": "Hành Trình Kỳ Thú",
      "description": "Câu chuyện phiêu lưu...",
      "genre": "Adventure, Fantasy",
      "coverImageUrl": "https://example.com/images/cover.jpg",
      "status": "Active"
    }
  ]
  ```

### 7.2 Get Ranking Board
* **Endpoint**: `GET http://localhost:5010/api/v1/ranking/board?votePeriod=2026-W23`
* **Response (200 OK)**:
  ```json
  [
    {
      "rank": 1,
      "seriesId": "87eb6ad6-6dcc-41e9-bf9e-f887d51f3c3a",
      "totalVotes": 1450,
      "votePeriod": "2026-W23"
    }
  ]
  ```

### 7.3 Get Chapters for a Series
* **Endpoint**: `GET http://localhost:5010/api/v1/chapters/series/{seriesId}`
* **Response (200 OK)**:
  ```json
  [
    {
      "chapterId": "23fa5f64-5717-4562-b3fc-2c963f66afb2",
      "title": "Chương 1: Khởi đầu mới",
      "chapterNumber": 1.0,
      "totalPages": 20,
      "status": "Approved"
    }
  ]
  ```
