# Thiết Kế State Machine & Ràng Buộc Trạng Thái Submission (Cập Nhật)

Tài liệu này chi tiết hóa thiết kế State Machine (Máy trạng thái) hoàn thiện cho thực thể **Submission** (Bản thảo) cùng hệ thống kiểm tra ràng buộc vai trò (Role-based) để đảm bảo an toàn nghiệp vụ, tránh lỗi `500 Internal Server Error` và trả về `400 Bad Request` chuẩn RESTful API.

---

## 1. Bản Đồ Chuyển Đổi Trạng Thái (State Transition Map)

Các trạng thái của thực thể:
*   **Draft**: Bản nháp do Mangaka khởi tạo.
*   **Pending_TE_Review**: Chờ Biên tập viên phụ trách (Tantou Editor - TE) kiểm duyệt.
*   **Pending_EB_Review**: Đã được TE thông qua, chờ Ban biên tập (Editorial Board - EB) duyệt xuất bản.
*   **Requires_Revision**: Ban biên tập hoặc Editor yêu cầu Mangaka chỉnh sửa lại.
*   **TE_Rejected**: Bản thảo bị Editor phụ trách từ chối chính thức (Trạng thái đóng băng).
*   **EB_Rejected**: Bản thảo bị Ban biên tập từ chối chính thức (Trạng thái đóng băng).
*   **EB_Approved**: Bản thảo được Ban biên tập phê duyệt xuất bản (Trạng thái đóng băng).

### Bảng Mapping API Endpoints & State Transitions (Đã Phân Tách Quyền)

| STT | API Endpoint | Vai Trò (Role) | Current State | Next State | Request Body / Tham Số | Mô tả Ràng buộc & Tác vụ |
|:---:|:---|:---|:---|:---|:---|:---|
| **1** | `POST /draft` | Mangaka | (None) | **Draft** | Tiêu đề, mô tả, thể loại, cover | Khởi tạo bản nháp mới. |
| **2** | `PUT /{id}/metadata` | Mangaka | **Draft**, **Requires_Revision** | Không đổi | Tiêu đề, mô tả, thể loại, cover | Chỉ cho phép sửa đổi thông tin khi ở 2 trạng thái này. Khóa ở các trạng thái khác. |
| **3** | `PUT /{id}/manuscript` | Mangaka | **Draft**, **Requires_Revision** | Không đổi | URL bản thảo mới | Chỉ cho phép tải lên file bản thảo khi ở 2 trạng thái này. |
| **4** | `POST /{id}/submit` | Mangaka | **Draft** | **Pending_TE_Review** | *(Trống)* | Nộp bản thảo lần đầu. Yêu cầu bắt buộc phải có URL bản thảo. |
| **5** | `POST /{id}/resubmit` | Mangaka | **Requires_Revision** | **Pending_TE_Review** | *(Trống)* | Mangaka nộp lại sau khi chỉnh sửa. Mở khóa thẩm định cho TE. |
| **6** | `POST /{id}/start-review`| TantouEditor | **Pending_TE_Review** | **Pending_TE_Review** | *(Trống)* | Đánh dấu TE đã nhận việc kiểm duyệt (không đổi trạng thái chính). |
| **7** | `POST /{id}/recommend` | TantouEditor | **Pending_TE_Review** | **Pending_EB_Review** | Ý kiến đề xuất | TE thẩm định đạt yêu cầu, chuyển tiếp lên EB phê duyệt. |
| **8a**| `POST /{id}/te-request-revision`| TantouEditor | **Pending_TE_Review** | **Requires_Revision** | `{"reason": "..."}` | TE yêu cầu sửa đổi. Trả quyền chỉnh sửa về cho Mangaka. |
| **8b**| `POST /{id}/eb-request-revision`| EditorialBoard | **Pending_EB_Review** | **Requires_Revision** | `{"reason": "..."}` | EB yêu cầu sửa đổi. Trả quyền chỉnh sửa về cho Mangaka. |
| **9a**| `POST /{id}/te-reject` | TantouEditor | **Pending_TE_Review** | **TE_Rejected** | `{"reason": "..."}` | TE từ chối bản thảo chính thức. Đóng băng bản thảo vĩnh viễn. |
| **9b**| `POST /{id}/eb-reject` | EditorialBoard | **Pending_EB_Review** | **EB_Rejected** | `{"reason": "..."}` | EB từ chối bản thảo chính thức. Đóng băng bản thảo vĩnh viễn. |
| **10**| `POST /{id}/approve` | EditorialBoard | **Pending_EB_Review** | **EB_Approved** | *(Trống)* | Ban biên tập phê duyệt. Tự động sinh `MangaSeries` & nâng cấp vai trò. |

---

## 2. Mã Nguồn C# Minh Họa (Kèm Bắt Lỗi Trạng Thái & Phân Quyền)

### 2.1 Định nghĩa Enum Trạng thái & Ngoại lệ Domain

```csharp
namespace MangaERP.Submission.Domain.Exceptions;

public class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string message) : base(message) { }
}
```

### 2.2 Domain Entity logic kiểm tra ràng buộc vai trò & trạng thái

```csharp
using MangaERP.Submission.Domain.Exceptions;

namespace MangaERP.Submission.Domain.Entities;

public enum SubmissionState
{
    Draft,
    Pending_TE_Review,
    Pending_EB_Review,
    Requires_Revision,
    TE_Rejected,
    EB_Rejected,
    EB_Approved
}

public class Submission
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string ManuscriptUrl { get; private set; }
    public SubmissionState State { get; private set; }
    public string FeedbackMessage { get; private set; }

    // Ràng buộc 1: KHÓA toàn bộ quyền chỉnh sửa khi đang trong hàng đợi duyệt
    public void UpdateMetadata(string newTitle, string newUrl)
    {
        if (State == SubmissionState.Pending_TE_Review || State == SubmissionState.Pending_EB_Review)
            throw new InvalidStateTransitionException("Không thể chỉnh sửa bản thảo khi đang trong quá trình xét duyệt.");

        if (State == SubmissionState.TE_Rejected || State == SubmissionState.EB_Rejected || State == SubmissionState.EB_Approved)
            throw new InvalidStateTransitionException("Bản thảo đã kết thúc quy trình xử lý và bị đóng băng.");

        Title = newTitle;
        ManuscriptUrl = newUrl;
    }

    public void Submit()
    {
        if (State != SubmissionState.Draft)
            throw new InvalidStateTransitionException("Chỉ có bản thảo ở trạng thái Draft mới được nộp lần đầu.");
        
        if (string.IsNullOrEmpty(ManuscriptUrl))
            throw new InvalidStateTransitionException("Vui lòng tải lên file bản thảo trước khi nộp.");

        State = SubmissionState.Pending_TE_Review;
    }

    public void Resubmit()
    {
        if (State != SubmissionState.Requires_Revision)
            throw new InvalidStateTransitionException("Chỉ có thể nộp lại bản thảo khi trạng thái là Requires_Revision.");

        State = SubmissionState.Pending_TE_Review;
    }

    public void RecommendToBoard()
    {
        if (State != SubmissionState.Pending_TE_Review)
            throw new InvalidStateTransitionException("Biên tập viên chỉ được phép đề xuất khi bản thảo đang chờ TE duyệt.");

        State = SubmissionState.Pending_EB_Review;
    }

    // Logic 8: Phân tách yêu cầu sửa đổi (Request Revision) theo vai trò và trạng thái
    public void RequestRevision(string actorRole, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidStateTransitionException("Lý do yêu cầu sửa đổi không được để trống.");

        if (actorRole == "TantouEditor")
        {
            if (State != SubmissionState.Pending_TE_Review)
                throw new InvalidStateTransitionException("Tantou Editor chỉ được yêu cầu sửa đổi khi bản thảo đang chờ TE duyệt.");
        }
        else if (actorRole == "EditorialBoard")
        {
            if (State != SubmissionState.Pending_EB_Review)
                throw new InvalidStateTransitionException("Editorial Board chỉ được yêu cầu sửa đổi khi bản thảo đang ở bước EB duyệt.");
        }
        else
        {
            throw new InvalidStateTransitionException("Vai trò này không có quyền yêu cầu sửa đổi bản thảo.");
        }

        State = SubmissionState.Requires_Revision;
        FeedbackMessage = reason;
    }

    // Logic 9: Phân tách từ chối (Reject) theo vai trò và trạng thái
    public void Reject(string actorRole, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidStateTransitionException("Lý do từ chối bản thảo không được để trống.");

        if (actorRole == "TantouEditor")
        {
            if (State != SubmissionState.Pending_TE_Review)
                throw new InvalidStateTransitionException("Tantou Editor chỉ được từ chối khi bản thảo đang chờ TE duyệt.");
            State = SubmissionState.TE_Rejected;
        }
        else if (actorRole == "EditorialBoard")
        {
            if (State != SubmissionState.Pending_EB_Review)
                throw new InvalidStateTransitionException("Editorial Board chỉ được từ chối khi bản thảo đang ở bước EB duyệt.");
            State = SubmissionState.EB_Rejected;
        }
        else
        {
            throw new InvalidStateTransitionException("Vai trò này không có quyền từ chối bản thảo.");
        }

        FeedbackMessage = reason;
    }

    public void Approve()
    {
        if (State != SubmissionState.Pending_EB_Review)
            throw new InvalidStateTransitionException("Chỉ có thể duyệt bản thảo khi đã được chuyển tiếp lên Ban Biên Tập.");

        State = SubmissionState.EB_Approved;
    }
}
```

### 2.3 Controller Bắt Lỗi Validation, Áp Dụng Phân Quyền Bảo Mật [Authorize]

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MangaERP.Submission.Domain.Exceptions;

namespace MangaERP.Submission.Presentation.Controllers;

[ApiController]
[Route("api/v1/submissions")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    // ... repository/mediator injection ...

    /// <summary>
    /// [TantouEditor] Đề xuất bản thảo đạt yêu cầu lên Ban biên tập (EB) phê duyệt.
    /// </summary>
    [HttpPost("{id:guid}/recommend")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Recommend(Guid id)
    {
        try
        {
            var submission = await _repo.GetByIdAsync(id);
            if (submission == null) return NotFound(new { message = "Không tìm thấy bản thảo." });

            submission.RecommendToBoard();
            await _repo.SaveChangesAsync();

            return Ok(new { message = "Đã đề xuất lên Ban Biên Tập thành công!" });
        }
        catch (InvalidStateTransitionException ex)
        {
            return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message });
        }
    }

    /// <summary>
    /// [TantouEditor] Yêu cầu sửa đổi bản thảo.
    /// </summary>
    [HttpPost("{id:guid}/te-request-revision")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> TeRequestRevision(Guid id, [FromBody] FeedbackRequest request)
    {
        try
        {
            var submission = await _repo.GetByIdAsync(id);
            if (submission == null) return NotFound(new { message = "Không tìm thấy bản thảo." });

            submission.RequestRevision("TantouEditor", request.Reason);
            await _repo.SaveChangesAsync();

            return Ok(new { message = "Đã yêu cầu sửa đổi thành công!" });
        }
        catch (InvalidStateTransitionException ex)
        {
            return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message });
        }
    }

    /// <summary>
    /// [EditorialBoard] Yêu cầu sửa đổi bản thảo.
    /// </summary>
    [HttpPost("{id:guid}/eb-request-revision")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> EbRequestRevision(Guid id, [FromBody] FeedbackRequest request)
    {
        try
        {
            var submission = await _repo.GetByIdAsync(id);
            if (submission == null) return NotFound(new { message = "Không tìm thấy bản thảo." });

            submission.RequestRevision("EditorialBoard", request.Reason);
            await _repo.SaveChangesAsync();

            return Ok(new { message = "Đã yêu cầu sửa đổi thành công!" });
        }
        catch (InvalidStateTransitionException ex)
        {
            return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message });
        }
    }

    /// <summary>
    /// [TantouEditor] Từ chối bản thảo.
    /// </summary>
    [HttpPost("{id:guid}/te-reject")]
    [Authorize(Roles = "TantouEditor")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TeReject(Guid id, [FromBody] FeedbackRequest request)
    {
        try
        {
            var submission = await _repo.GetByIdAsync(id);
            if (submission == null) return NotFound(new { message = "Không tìm thấy bản thảo." });

            submission.Reject("TantouEditor", request.Reason);
            await _repo.SaveChangesAsync();

            return Ok(new { message = "Đã từ chối bản thảo thành công." });
        }
        catch (InvalidStateTransitionException ex)
        {
            return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message });
        }
    }

    /// <summary>
    /// [EditorialBoard] Từ chối bản thảo.
    /// </summary>
    [HttpPost("{id:guid}/eb-reject")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> EbReject(Guid id, [FromBody] FeedbackRequest request)
    {
        try
        {
            var submission = await _repo.GetByIdAsync(id);
            if (submission == null) return NotFound(new { message = "Không tìm thấy bản thảo." });

            submission.Reject("EditorialBoard", request.Reason);
            await _repo.SaveChangesAsync();

            return Ok(new { message = "Đã từ chối bản thảo thành công." });
        }
        catch (InvalidStateTransitionException ex)
        {
            return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message });
        }
    }

    /// <summary>
    /// [EditorialBoard] Phê duyệt chính thức bản thảo.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "EditorialBoard")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Approve(Guid id)
    {
        try
        {
            var submission = await _repo.GetByIdAsync(id);
            if (submission == null) return NotFound(new { message = "Không tìm thấy bản thảo." });

            submission.Approve();
            await _repo.SaveChangesAsync();

            return Ok(new { message = "Bản thảo đã được phê duyệt thành công!" });
        }
        catch (InvalidStateTransitionException ex)
        {
            return BadRequest(new { error = "Lỗi quy trình nghiệp vụ", message = ex.Message });
        }
    }
}

public record FeedbackRequest(string Reason);
```
