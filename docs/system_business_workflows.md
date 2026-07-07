# 📖 Tổng Quan Nghiệp Vụ Hệ Thống MangaERP

Tài liệu này tổng hợp toàn bộ các luồng nghiệp vụ chính, quy tắc kiểm duyệt, phân quyền vai trò (RBAC) và cơ chế vận hành của hệ thống MangaERP.

---

## 👤 1. Các Vai Trò Trong Hệ Thống (RBAC Roles)

Hệ thống quản lý phân quyền chặt chẽ dựa trên vai trò nghiệp vụ thực tế của từng nhân sự:

| Vai trò | Viết tắt | Mô tả & Giới hạn nghiệp vụ |
|---|---|---|
| **System Admin** | Admin | **Quản trị viên hạ tầng kỹ thuật.** Tạo tài khoản, cấu hình tham số hệ thống. **Không** tham gia vào bất kỳ nghiệp vụ nội dung hay phê duyệt nào. |
| **Editorial Board** | EB | **Ban biên tập.** Bỏ phiếu duyệt bản thảo truyện mới, duyệt yêu cầu dừng/hủy truyện, quản lý lịch phát hành. |
| **Editor-in-Chief** | EiC | **Tổng biên tập.** Có đầy đủ quyền của EB và là **trọng tài tối cao** quyết định kết quả cuối cùng khi EB bỏ phiếu bất đồng thuận (xung đột 1-1-1). |
| **Tantou Editor** | TE | **Biên tập viên phụ trách.** Đồng hành trực tiếp cùng Mangaka, giao việc, kiểm tra chất lượng bản thảo từng trang và thực hiện ghim lỗi QA. |
| **Mangaka** | Artist | **Họa sĩ chính (Tác giả).** Đề xuất series truyện mới, quản lý studio vẽ của mình, chia việc cho Trợ lý và sửa lỗi QA. |
| **Assistant** | Trợ lý | **Trợ lý vẽ.** Tham gia vào Studio của Mangaka, nhận các task vẽ layer (LineArt, tô màu, text) và nộp sản phẩm vẽ. |

---

## ⚙️ 2. Chi Tiết Nghiệp Vụ Theo Từng Module

### 🛡️ 2.1 Module Identity & Auth (Quản lý Danh tính & Bảo mật)
*   **Cấp tài khoản (Provisioning):** Admin tạo tài khoản cho nhân sự mới bằng cách điền thông tin và Email cá nhân (`PersonalEmail`). Hệ thống tự sinh tài khoản nội bộ dạng `username.mgk@company.com` và gửi link kích hoạt qua Email cá nhân của họ.
*   **Kích hoạt tài khoản (Activation):** Người dùng truy cập link kích hoạt, tự thiết lập mật khẩu và chuyển trạng thái tài khoản từ `PendingActivation` sang `Active`.
*   **Xác thực Cookie-based:**
    *   Sau khi `/login` thành công, Access Token (JWT) được trả về trong JSON body và lưu ngắn hạn tại bộ nhớ JavaScript của client.
    *   Refresh Token được tự động ghi vào cookie dạng **httpOnly, Secure, SameSite=Lax (dev) / None (prod)** để tự động gia hạn phiên đăng nhập (Silent Refresh).
*   **Đăng xuất an toàn (Token Blacklist):** Khi `/logout`, hệ thống ghi JTI (Jwt ID) của Access Token hiện tại vào `IMemoryCache` (với TTL bằng thời gian sống còn lại của Token) để vô hiệu hóa nó ngay lập tức.
*   **Yêu cầu quên mật khẩu (OTP):** OTP 6 số được gửi về Email cá nhân của người dùng. OTP được mã hóa SHA256 trước khi lưu cache, giới hạn nhập sai tối đa 5 lần để chống Brute-force.

---

### 📝 2.2 Module Submission (Duyệt Đề xuất Truyện mới - Luồng 1 Tầng)
*   **Khởi tạo đề xuất (Draft):** Mangaka tạo bản thảo truyện nháp bao gồm tiêu đề, mô tả, thể loại, ảnh bìa và tải lên URL tập tin bản thảo vẽ nháp (`ManuscriptUrl`).
*   **Nộp bản thảo (Submit):** Bản thảo được gửi duyệt, chuyển trạng thái sang `Pending_EB_Review`.
*   **Bỏ phiếu tập thể (Collective Voting):**
    *   Mỗi thành viên trong Ban biên tập (EB) xem danh sách hàng đợi duyệt và tiến hành bỏ phiếu (`APPROVE`, `REJECT`, hoặc `REQ_REVISION`).
    *   Mỗi người chỉ được vote 1 lần mỗi vòng. Cần tối đa 3 phiếu bầu để kích hoạt bộ gom phiếu tự động (Aggregation):
        *   **Đồng thuận (Thiểu số phục tùng đa số):** Có $\ge 2$ phiếu cùng loại $\rightarrow$ Áp dụng kết quả đó.
            *   `APPROVE` $\rightarrow$ Chuyển trạng thái sang `EB_Approved`. Tự động tạo `MangaSeries` mới và chỉ định một Biên tập viên phụ trách (Tantou Editor) cho Mangaka.
            *   `REJECT` $\rightarrow$ Đóng băng bản thảo ở trạng thái `EB_Rejected`.
            *   `REQ_REVISION` $\rightarrow$ Chuyển trạng thái sang `Requires_Revision` kèm danh sách ghim lỗi trực quan (Visual Pins) của EB trên bản thảo.
        *   **Bất đồng thuận (Xung đột):** Nhận 3 phiếu khác nhau hoàn toàn (1 Approve, 1 Reject, 1 Revision) $\rightarrow$ Trạng thái chuyển sang `Conflict_Escalated`.
*   **Trọng tài phân xử (Arbitration):** Tổng biên tập (EiC) kiểm tra các đề xuất bị `Conflict_Escalated` và đưa ra phán quyết cuối cùng (Approve / Reject / Request Revision) để phá vỡ thế bế tắc.
*   **Gửi lại bản thảo (Resubmit):** Mangaka sửa đổi bản vẽ dựa trên phản hồi của EB và nộp lại. Quy trình bỏ phiếu của EB được reset sang vòng mới (`Round++`).

---

### 📚 2.3 Module Series (Quản lý Series & Vòng đời Truyện)
*   **Tạo Series tự động:** Sau khi đề xuất được duyệt thông qua, thực thể `MangaSeries` được tạo tự động và liên kết trực tiếp với Mangaka cùng Tantou Editor được chỉ định.
*   **Quản lý trạng thái Series:**
    *   **Hiatus (Tạm ngưng):** Đưa truyện vào trạng thái tạm dừng sáng tác khi họa sĩ gặp vấn đề sức khỏe hoặc lý do bất khả kháng.
    *   **Reactivate (Kích hoạt lại):** Khôi phục trạng thái hoạt động bình thường khi họa sĩ quay trở lại sáng tác.
*   **Yêu cầu hủy truyện (Cancellation Flow):**
    *   Mangaka gửi yêu cầu xin dừng/hủy truyện hẳn (`cancellation-request`).
    *   Yêu cầu được chuyển vào hàng đợi duyệt của Ban biên tập (`cancellation-queue`).
    *   Chỉ EB hoặc EiC mới có quyền phê duyệt hoặc từ chối yêu cầu hủy truyện. Admin không can thiệp.

---

### 🎨 2.4 Module Studio (Quản lý Đội ngũ vẽ phụ trợ)
*   **Lời mời gia nhập (Studio Invitations):** Mangaka mời các trợ lý (Assistant) tham gia vào Studio vẽ của một Series truyện thông qua username/email.
*   **Xử lý lời mời:** Trợ lý kiểm tra thông báo và bấm Đồng ý (Accept) hoặc Từ chối (Decline).
*   **Khai trừ trợ lý:** Mangaka có quyền xóa trợ lý ra khỏi Studio vẽ của Series. Khi khai trừ, hệ thống tự động quét và thu hồi toàn bộ nhiệm vụ (Task) vẽ trang mà trợ lý đó đang làm dở để trả về trạng thái chờ phân công.

---

### 📖 2.5 Module Chapter (Lên kế hoạch Chapter)
*   **Tạo Chapter:** Mangaka tạo chương truyện mới cho Series của mình dưới dạng `Draft`.
*   **Tự động gán Biên tập viên:** Để tránh việc Mangaka tự ý chọn Biên tập viên khác, hệ thống tự động truy vấn `ManagingTantouId` từ profile của Mangaka và gán trực tiếp làm người duyệt chapter đó.

---

### ✏️ 2.6 Module Task (Phân rã Trang vẽ & Vẽ Layer kỹ thuật)
*   **Phân rã trang vẽ:** Mangaka tải lên các trang phác thảo thô (Storyboard/Name) của chapter. Hệ thống tự động tạo ra các Task tương ứng với từng trang đơn lẻ.
*   **Phân tách Layer kỹ thuật:** Mỗi trang vẽ được chia thành 3 lớp vẽ độc lập:
    1.  **LineArt Layer:** Nét vẽ nhân vật, bối cảnh.
    2.  **Coloring Layer:** Lớp đổ màu, tô bóng, screentone.
    3.  **Text Layer:** Lớp thoại, hội thoại nhân vật và hiệu ứng chữ (SFX).
*   **Giao việc vẽ Layer:** Mangaka phân công từng layer của trang vẽ cho các trợ lý (Assistant) vẽ phụ trợ.
*   **Nộp sản phẩm:** Trợ lý vẽ trực tiếp trên phần mềm chuyên dụng và tải ảnh sản phẩm lên đúng layer được giao.
*   **Duyệt hàng loạt (Bulk Review):** Mangaka kiểm duyệt nhanh các layer mà trợ lý nộp lên. Đồng ý hoặc Từ chối kèm ghi chú sửa lỗi (`RejectionNote`).
*   **Lịch sử phiên bản (Versioning & Rollback):** Hệ thống ghi nhận tất cả phiên bản vẽ cũ của layer, cho phép họa sĩ hoặc trợ lý khôi phục (Rollback) về phiên bản cũ bất kỳ lúc nào nếu vẽ hỏng.

---

### 🔍 2.7 Module QA (Kiểm duyệt Chất lượng Chapter)
*   **Khởi tạo phiên QA:** Khi chapter hoàn thành 100% các layer của tất cả các trang vẽ, Mangaka gửi duyệt chapter sang trạng thái chờ duyệt. Phiên QA được mở tự động cho Tantou Editor (TE).
*   **Ghim lỗi trực quan (Visual Pins):**
    *   Tantou Editor mở canvas kiểm tra các trang vẽ của chapter.
    *   Khi phát hiện lỗi (ví dụ: sai chính tả thoại, tô màu lệch, đè nét vẽ), TE nhấp chuột trực tiếp lên vị trí lỗi trên ảnh để tạo một **Ghim lỗi (QaPin)** kèm bình luận và phân loại lỗi.
*   **Gửi phản hồi sửa lỗi (Send Feedback Batch):** TE gửi toàn bộ danh sách ghim lỗi về cho Mangaka sửa.
*   **Sửa lỗi & Báo cáo hoàn thành (Fix & Mark Resolved):** Mangaka nhận danh sách ghim lỗi, sửa trực tiếp trên file vẽ, upload bản sửa và đánh dấu ghim lỗi đã được sửa xong.
*   **Mở lại phiên QA (Reopen QA):** Nếu Mangaka báo đã sửa nhưng TE kiểm tra lại thấy vẫn lỗi hoặc sót lỗi khác, TE có quyền reopen phiên QA và ghim lỗi mới.
*   **Duyệt QA:** Khi tất cả các ghim lỗi được giải quyết hoàn toàn, TE duyệt thông qua chapter (`ChapterApproved`). Hệ thống tự động bắn notification báo cho Ban Biên tập biết chapter đã sẵn sàng lên lịch xuất bản.

---

### 📅 2.8 Module Publishing (Lên lịch & Tự động xuất bản)
*   **Hàng đợi xuất bản (Ready Queue):** Ban biên tập (EB) truy cập danh sách các chapter đã qua QA kiểm duyệt (`Approved`) đang chờ được phát hành.
*   **Lên lịch xuất bản:** EB đặt ngày giờ phát hành cụ thể cho chapter. Lịch trình được hiển thị trực quan dưới dạng Calendar (Lịch biểu tuần/tháng).
*   **Hủy lịch xuất bản:** EB có quyền rút lại/hủy lịch phát hành đã đặt trong trường hợp khẩn cấp.
*   **Tự động xuất bản (Scheduled Job):** Hệ thống sử dụng background worker quét định kỳ các chapter đã đến giờ phát hành để chuyển trạng thái sang `Published` và hiển thị công khai cho độc giả.
*   **Chống xung đột thứ tự xuất bản:** Kiểm tra ngày giờ xuất bản để tránh việc chapter sau xuất bản trước chapter trước.

---

### 📊 2.9 Module Ranking (Bảng xếp hạng)
*   **Thu thập tương tác:** Lưu trữ dữ liệu bình chọn, số lượt xem và thả tim của độc giả cho từng Series truyện.
*   **Tính điểm xếp hạng định kỳ:** Background job chạy định kỳ (tuần/tháng) tổng hợp dữ liệu tương tác để biên soạn bảng xếp hạng truyện.

---

### 🧠 2.10 Module Segmentation (Trợ lý Phân vùng AI - SAM)
*   **AI Auto-Segment:** Sử dụng embeddings của mô hình AI Segment Anything (SAM) để giúp Mangaka click chọn và tự động cắt các vùng nhân vật/khung tranh thành các RLE masks.
*   **Khóa vùng vẽ cho Assistant:** Mask của phân vùng được gửi kèm khi phân công task cho Assistant. Client/Vẽ Canvas sẽ chặn nét cọ vẽ ra ngoài biên của mask để tránh trợ lý vẽ lem ra ngoài khung hình được giao.
