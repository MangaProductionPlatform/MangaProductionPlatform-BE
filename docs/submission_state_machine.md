# Thiết Kế State Machine & Ràng Buộc Trạng Thái Submission (Cập Nhật: 1-Tier Review)

Tài liệu này chi tiết hóa thiết kế State Machine cho thực thể **Submission** (Bản thảo) sau khi refactor sang **luồng duyệt 1 tầng** (Editorial Board duyệt trực tiếp, không qua Tantou Editor).

---

## 1. Bản Đồ Chuyển Đổi Trạng Thái (State Transition Map)

Các trạng thái của thực thể:
*   **Draft**: Bản nháp do Mangaka khởi tạo.
*   **Pending_EB_Review**: Chờ Ban biên tập (Editorial Board - EB) kiểm duyệt trực tiếp.
*   **Requires_Revision**: EB yêu cầu Mangaka chỉnh sửa lại (kèm Visual Feedback Pins).
*   **EB_Rejected**: Bản thảo bị Ban biên tập từ chối chính thức (Trạng thái đóng băng).
*   **EB_Approved**: Bản thảo được Ban biên tập phê duyệt xuất bản (Trạng thái đóng băng). Tự động tạo `MangaSeries` và gán Tantou Editor.

> **Lưu ý:** Các trạng thái `Pending_TE_Review` và `TE_Rejected` đã bị loại bỏ khỏi luồng.

### Bảng Mapping API Endpoints & State Transitions

| STT | API Endpoint | Vai Trò (Role) | Current State | Next State | Request Body | Mô tả |
|:---:|:---|:---|:---|:---|:---|:---|
| **1** | `POST /draft` | Mangaka | (None) | **Draft** | Tiêu đề, mô tả, thể loại, cover | Khởi tạo bản nháp mới. |
| **2** | `PUT /{id}/metadata` | Mangaka | **Draft**, **Requires_Revision** | Không đổi | Tiêu đề, mô tả, thể loại, cover | Chỉ sửa metadata khi ở 2 trạng thái này. |
| **3** | `PUT /{id}/manuscript` | Mangaka | **Draft**, **Requires_Revision** | Không đổi | URL bản thảo mới | Chỉ upload bản thảo khi ở 2 trạng thái này. |
| **4** | `POST /{id}/submit` | Mangaka | **Draft** | **Pending_EB_Review** | *(Trống)* | Nộp bản thảo lần đầu. Yêu cầu có ManuscriptUrl. |
| **5** | `POST /{id}/resubmit` | Mangaka | **Requires_Revision** | **Pending_EB_Review** | *(Trống)* | Nộp lại sau chỉnh sửa. |
| **6** | `POST /{id}/request-revision` | EditorialBoard | **Pending_EB_Review** | **Requires_Revision** | `{reason, pins[]}` | EB yêu cầu sửa đổi kèm Visual Feedback Pins trên canvas. |
| **7** | `POST /{id}/reject` | EditorialBoard | **Pending_EB_Review** | **EB_Rejected** | `{reason}` | EB từ chối chính thức. Đóng băng vĩnh viễn. |
| **8** | `POST /{id}/approve` | EditorialBoard | **Pending_EB_Review** | **EB_Approved** | `{assignedEditorId}` | EB phê duyệt. Tạo `MangaSeries` + gán Tantou Editor cho Mangaka. |

### Sơ Đồ Trạng Thái

```
Draft ──submit──> Pending_EB_Review ──approve──> EB_Approved (đóng băng)
                       │                           ↑
                       ├──reject──> EB_Rejected     │  (đóng băng)
                       │                            │
                       └──request-revision──> Requires_Revision ──resubmit──┘
```

---

## 2. Mã Nguồn C# Minh Họa (Luồng 1 Tầng)

### 2.1 Định nghĩa Enum Trạng thái & Ngoại lệ Domain

```csharp
namespace MangaERP.Submission.Domain.Exceptions;

public class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string message) : base(message) { }
}
```

### 2.2 Domain Entity — Kiểm tra ràng buộc trạng thái & vai trò

```csharp
public enum SubmissionStatus
{
    Draft,
    Pending_EB_Review,
    Requires_Revision,
    EB_Rejected,
    EB_Approved
}

public class SeriesSubmission
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string ManuscriptUrl { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public string FeedbackMessage { get; private set; }

    // Chỉ cho phép sửa metadata khi Draft hoặc Requires_Revision
    public void UpdateMetadata(string newTitle)
    {
        if (Status == SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Không thể chỉnh sửa khi đang chờ duyệt.");
        if (Status == SubmissionStatus.EB_Rejected || Status == SubmissionStatus.EB_Approved)
            throw new InvalidStateTransitionException("Bản thảo đã đóng băng.");
        Title = newTitle;
    }

    // Draft → Pending_EB_Review
    public void SubmitDraft()
    {
        if (Status != SubmissionStatus.Draft)
            throw new InvalidStateTransitionException("Chỉ bản nháp mới được nộp.");
        if (string.IsNullOrEmpty(ManuscriptUrl))
            throw new InvalidStateTransitionException("Phải upload bản thảo trước khi nộp.");
        Status = SubmissionStatus.Pending_EB_Review;
    }

    // Requires_Revision → Pending_EB_Review
    public void ReSubmit()
    {
        if (Status != SubmissionStatus.Requires_Revision)
            throw new InvalidStateTransitionException("Chỉ nộp lại khi trạng thái Requires_Revision.");
        Status = SubmissionStatus.Pending_EB_Review;
    }

    // Pending_EB_Review → Requires_Revision (CHỈ EditorialBoard)
    public void RequestRevision(string actorRole, Guid reviewerId, string reason)
    {
        if (actorRole != "EditorialBoard")
            throw new InvalidStateTransitionException("Chỉ Editorial Board mới được yêu cầu sửa đổi.");
        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Bản thảo phải ở trạng thái Pending_EB_Review.");
        Status = SubmissionStatus.Requires_Revision;
        FeedbackMessage = reason;
    }

    // Pending_EB_Review → EB_Rejected (CHỈ EditorialBoard)
    public void Reject(string actorRole, Guid reviewerId, string reason)
    {
        if (actorRole != "EditorialBoard")
            throw new InvalidStateTransitionException("Chỉ Editorial Board mới được từ chối bản thảo.");
        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Bản thảo phải ở trạng thái Pending_EB_Review.");
        Status = SubmissionStatus.EB_Rejected;
        FeedbackMessage = reason;
    }

    // Pending_EB_Review → EB_Approved (CHỈ EditorialBoard)
    public void Approve(Guid reviewerId)
    {
        if (Status != SubmissionStatus.Pending_EB_Review)
            throw new InvalidStateTransitionException("Chỉ duyệt khi ở trạng thái Pending_EB_Review.");
        Status = SubmissionStatus.EB_Approved;
    }
}
```

### 2.3 Controller — API Endpoints (Luồng 1 Tầng)

```csharp
[ApiController]
[Route("api/v1/submissions")]
public class SubmissionsController : ControllerBase
{
    // ── MANGAKA FLOWS ────────────────────────────────────────
    [HttpPost("draft")]              [Authorize(Roles = "Mangaka")]
    [HttpPut("{id}/metadata")]       [Authorize(Roles = "Mangaka")]
    [HttpPut("{id}/manuscript")]     [Authorize(Roles = "Mangaka")]
    [HttpPost("{id}/submit")]        [Authorize(Roles = "Mangaka")]
    [HttpPost("{id}/resubmit")]      [Authorize(Roles = "Mangaka")]
    [HttpGet("my")]                  [Authorize(Roles = "Mangaka")]

    // ── EDITORIAL BOARD FLOWS ────────────────────────────────
    [HttpGet("queue")]               [Authorize(Roles = "EditorialBoard,Admin")]
    [HttpPost("{id}/request-revision")] [Authorize(Roles = "EditorialBoard")]  // kèm Visual Feedback Pins
    [HttpPost("{id}/reject")]        [Authorize(Roles = "EditorialBoard")]
    [HttpPost("{id}/approve")]       [Authorize(Roles = "EditorialBoard")]     // kèm AssignedEditorId

    // ── SHARED FLOWS ─────────────────────────────────────────
    [HttpGet("{id}")]                [Authorize(Roles = "Mangaka,TantouEditor,EditorialBoard,Admin")]
}
```
