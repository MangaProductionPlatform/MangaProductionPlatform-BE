# 🛡️ MangaERP — Guardrails Kỹ Thuật Khi Vibe Coding

> Mục tiêu: liệt kê các **luật cứng (hard rules)** theo từng module/người phụ trách, để khi dùng Antigravity/AI sinh code, AI không lặp lại các lỗi bảo mật/kiến trúc đã từng mắc (vd: lưu refreshToken vào localStorage).
> Cách dùng: dán nguyên phần tương ứng vào prompt cho AI trước khi yêu cầu code, hoặc dùng làm checklist review PR.

---

## 0. Luật chung — Áp dụng cho TẤT CẢ mọi người

### 0.1 Auth & Token (đã từng sai — không lặp lại)
- ❌ **KHÔNG** bao giờ lưu `accessToken`/`refreshToken` vào `localStorage` hoặc `sessionStorage`.
- ✅ `accessToken`: chỉ giữ **in-memory** (biến JS/state, ví dụ Zustand/Context store không persist), mất khi F5 → phải gọi `/auth/refresh` lại lúc app khởi động.
- ✅ `refreshToken`: chỉ tồn tại trong **httpOnly cookie** do BE set, FE không bao giờ đọc/ghi được bằng JS.
- ✅ Mọi request gọi BE phải có `credentials: "include"`.
- ❌ Không log `accessToken`/`refreshToken` ra console, kể cả khi debug.
- ✅ Cookie refreshToken phải có `Secure`, `HttpOnly`, `SameSite=Strict` hoặc `Lax` (tùy có cross-site call không).

### 0.2 Secrets & Config
- ❌ Không hardcode connection string, API key, JWT secret trong code.
- ✅ Đọc từ `appsettings.json` + `appsettings.Development.json` (gitignore) hoặc biến môi trường.
- ❌ Không commit file `.env`, `appsettings.Development.json`, key Google Colab API vào git.

### 0.3 CQRS/MediatR — không phá pattern khi AI sinh code nhanh
- ✅ Mỗi Command/Query luôn có Validator riêng (FluentValidation), không validate tay trong Handler.
- ✅ Handler chỉ gọi đúng Repository thuộc module của nó. Nếu cần data module khác → gọi qua interface/event, không inject thẳng DbContext của module khác.
- ⚠️ **Nhắc lại Bug #1 đã gặp:** nếu Handler động vào 2 aggregate khác nhau (vd PageTask + ArtworkLayer), phải gọi `SaveChangesAsync()` của **từng repo liên quan**, không chỉ 1 cái. Tốt nhất: dùng chung 1 `IUnitOfWork.SaveChangesAsync()` thay vì save riêng lẻ từng repo để tránh quên.
- ✅ Status/state của entity phải lấy từ **field/enum thật**, không suy luận ngược từ field khác (vd Bug #2: suy `Status` từ `RejectionNote == null`).

### 0.6 Concurrency — chọn đúng pattern theo từng trường hợp (verified từ CastVote & ApproveSubmission)
- ✅ Khi action cần **lock 1 row cụ thể** (vd: bỏ phiếu cho 1 submission): dùng `SELECT ... FOR UPDATE` qua `GetByIdForUpdateAsync` — chỉ block row đó, không block table, hiệu quả hơn Serializable trong trường hợp này.
- ✅ Khi action cần đọc **aggregate across nhiều rows** và ra quyết định dựa vào đó (vd: tìm TantouEditor có load thấp nhất để gán): dùng `IsolationLevel.Serializable` để ngăn phantom read — hai thread concurrent không thể nhìn thấy cùng snapshot stale.
- ❌ Không dùng `Serializable` mặc định cho mọi Handler — gây lock contention không cần thiết.
- ✅ Bọc manual transaction trong `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — bắt buộc khi dùng `NpgsqlRetryingExecutionStrategy`.

### 0.4 Database & Migration
- ❌ Không tự ý sửa `AppDbContext.cs` hoặc `SharedInfrastructureExtensions.cs` mà không báo nhóm.
- ✅ Ai thêm entity/column → người đó tạo migration, đặt tên migration rõ ràng (`AddXxxToYyy`, không để `Migration1`).
- ❌ Không dùng raw SQL nếu LINQ/EF Core làm được, tránh SQL injection khi AI sinh code nhanh.

### 0.5 N+1 & Performance (nhắc lại Minor #4)
- ❌ Không loop gọi Repository bên trong vòng `for`/`foreach` khi xử lý danh sách (bulk action).
- ✅ Luôn có method `GetByXxxAsync(ids[])` nhận mảng ID, trả về 1 query duy nhất, rồi xử lý trong memory.

---

## 1. Bao — MF1 (Submission/Series) + Core Phần 3 (Admin, S3, Middleware)

### 1.1 S3/File Storage
- ❌ Không cho FE upload thẳng lên S3 với credentials lộ ra client (access key/secret key).
- ✅ Dùng **pre-signed URL**: FE gọi BE xin URL có thời hạn ngắn (5–15 phút), FE upload trực tiếp lên S3 bằng URL đó.
- ✅ Giới hạn loại file (`image/png`, `image/jpeg`, `.psd` nếu cần) và kích thước tối đa ở cả FE lẫn BE (không tin FE).
- ✅ Tên file lưu trên S3 phải là GUID, không dùng tên gốc người dùng (tránh path traversal, đụng tên).
- ❌ Không để bucket S3 public-read toàn bộ; chỉ public với asset đã duyệt (ảnh bìa, trang đã publish), còn bản thảo/raw phải private + signed URL khi cần xem.

### 1.2 Admin Endpoints
- ✅ Mọi route `/api/v1/admin/*` phải có `[Authorize(Roles = "Admin")]` ở **cả Controller lẫn Handler** (defense in depth), không chỉ check ở FE.
- ✅ Endpoint xem toàn bộ dữ liệu (không filter theo userId) phải log vào Audit Log — ai gọi, lúc nào.
- ❌ Không trả về thông tin nhạy cảm (password hash, token, email người khác) trong response Admin Dashboard nếu không cần thiết.

### 1.3 Audit Log & Exception Handler
- ✅ Audit log ghi tối thiểu: `UserId, Action, Entity, EntityId, Timestamp, IP (nếu có)` — không ghi nội dung nhạy cảm dạng plaintext (vd password).
- ✅ Global Exception Handler: bắt exception, trả JSON chuẩn `{ statusCode, message, traceId }`, **không** trả raw stack trace hoặc connection string ra ngoài cho client ở môi trường Production.
- ✅ Phân biệt response lỗi Dev (chi tiết) vs Production (ẩn chi tiết kỹ thuật).

---

## 2. Nam — MF2 (Chapter/Task) + Core Phần 1 (Identity, Notifications, SignalR, Email, RateLimiter)

### 2.1 Identity / Auth (trọng tâm — đây là chỗ đã sai)
- ✅ Áp dụng nguyên 100% mục 0.1 ở trên — đây là module Nam sở hữu nên Nam là người enforce.
- ✅ `accessToken` thời hạn ngắn (5–15 phút), `refreshToken` thời hạn dài hơn (vd 7 ngày) nhưng phải **rotate**: mỗi lần refresh thành công, cấp refreshToken mới + revoke token cũ (chống replay nếu token cũ bị đánh cắp).
- ✅ Lưu refreshToken (hash, không lưu plaintext) trong DB kèm `ExpiresAt`, `RevokedAt`, `ReplacedByToken` để truy vết.
- ❌ Không trả refreshToken qua body hoặc query string trong bất kỳ trường hợp nào, kể cả lúc debug.
- ✅ Logout phải revoke refreshToken ở DB + xóa cookie (`Set-Cookie` với `Max-Age=0`), không chỉ xóa ở FE.

### 2.2 SignalR Hub
- ✅ Hub endpoint phải `[Authorize]`, chỉ user đã login mới connect được.
- ✅ Group theo `userId` (vd `Groups.AddToGroupAsync(connectionId, $"user-{userId}")`), không broadcast toàn bộ cho tất cả client.
- ❌ Không gửi dữ liệu nhạy cảm qua SignalR message nếu không cần (vd không gửi nguyên object User kèm email/role của người khác).
- ⚠️ **SignalR transport fallback & CORS:** Khi WebSocket bị proxy/firewall chặn ở production, SignalR tự động fallback xuống **Server-Sent Events → long-polling** — cả hai đều đi qua browser CORS, trong khi WebSocket thì không. Hậu quả: "connect OK khi dev (WebSocket) nhưng fail ở production sau proxy". Phải đảm bảo CORS policy (origin cụ thể + `AllowCredentials()`) được áp dụng đúng cho mọi transport. Thêm `.AllowAnyHeader()` theo khuyến nghị Microsoft cho SignalR negotiate headers.
- ✅ **Test bắt buộc trước khi merge Hub code:** kiểm tra kết nối qua cả 3 transport — WebSocket, SSE, long-polling.

### 2.3 Email Service
- ✅ Link trong email reset password phải có token thời hạn ngắn (vd 15–30 phút), token random đủ dài (không dùng userId làm token).
- ❌ Không gửi password (cũ hoặc tạm) qua email dạng plaintext.

### 2.4 Rate Limiter
- ✅ Áp dụng cho `/auth/login`, `/auth/refresh`, `/auth/forgot-password`, `POST .../vote` — đây là các endpoint dễ bị spam/brute-force.
- ✅ Rate limit theo cả IP và theo account (username) để chống brute-force lẫn credential stuffing.

### 2.5 Task/Chapter Handler (liên quan Bug #1 đã gặp)
- ✅ Khi 1 action động vào nhiều aggregate (PageTask + ArtworkLayer + Notification), review kỹ phần `SaveChangesAsync` — viết test cho case "duyệt layer xong, query lại PageTask phải thấy status mới".

---

## 3. Bach — MF3 (QA/Publishing) + Core Phần 2 (Ranking, Background Jobs)

### 3.1 Scheduled Publisher Job (Quartz/Hangfire)
- ✅ Job phải **idempotent** — chạy lại nhiều lần (do retry/crash) không được publish trùng/gửi notification trùng. Dùng flag `IsPublished`/`PublishedAt` để check trước khi xử lý.
- ✅ Dùng cơ chế lock (DB lock hoặc Hangfire's built-in `[DisableConcurrentExecution]`) nếu chạy nhiều instance, tránh 2 job chạy song song publish trùng.
- ✅ Log lại job run (thành công/thất bại) để debug khi chapter không tự publish đúng giờ.

### 3.2 Ranking Module (data-heavy)
- ❌ Không cho phép import vote data trùng kỳ (`period`) mà không có cảnh báo — phải check tồn tại trước, hoặc dùng upsert có chủ đích.
- ✅ Compile ranking nên chạy trong 1 transaction — nếu lỗi giữa chừng, rollback toàn bộ, không để Snapshot bị half-done.
- ✅ Endpoint `DELETE /ranking/import/{period}` chỉ cho Admin, và phải chặn nếu kỳ đó **đã compile** thành Snapshot rồi (tránh xóa dữ liệu nguồn của báo cáo đã công bố).
- ✅ `GET /ranking/board` là endpoint public → không expose dữ liệu vote thô (ai vote gì), chỉ trả kết quả tổng hợp.

### 3.3 QA & Publishing Schedule
- ✅ `PATCH/DELETE /publishing/schedule/{id}` chỉ EB, và phải chặn sửa/xóa nếu chapter đã ở trạng thái `Published` rồi (tránh sửa nhầm cái đã lên sóng).
- ✅ Pin QA (`PATCH/DELETE /qa/pins/{pinId}`) chỉ cho phép người tạo pin hoặc TE của chapter đó sửa/xóa — check ownership trong Handler, không chỉ check role.

---

## 4. Frontend chung (mangaErpService.ts, httpClient.ts, ...)

- ✅ Tách rõ 2 loại storage:
  - **In-memory** (state/store, mất khi F5): `accessToken`.
  - **localStorage** (chỉ chứa thông tin KHÔNG nhạy cảm, dùng để hiển thị UI khi F5): `{ email, userId, role }` — tuyệt đối không thêm token vào object này.
- ✅ Khi gọi API mà nhận `401`, FE tự động gọi `/auth/refresh` 1 lần, nếu vẫn fail thì logout — tránh loop vô hạn refresh.
- ❌ Không catch lỗi rồi nuốt silent (`catch(e) {}`) ở các luồng auth — phải có log/toast để dễ debug khi AI sinh code thiếu xử lý lỗi.
- ✅ Mọi component hiển thị data theo role (Admin/EB/Mangaka...) vẫn phải có guard ở BE — FE-only guard không phải bảo mật, chỉ là UX.

---

## 5. Checklist nhanh khi review PR (dán vào prompt AI trước khi merge)

```
[ ] Không có chuỗi "localStorage" hoặc "sessionStorage" gần biến chứa token
[ ] Mọi fetch/axios call có credentials: "include" (nếu gọi BE cần cookie)
[ ] Handler nào động > 1 repository → có gọi đủ SaveChangesAsync hoặc dùng UnitOfWork
[ ] Status/Enum lấy từ field thật, không suy luận từ field phụ
[ ] Bulk action không loop query trong vòng for
[ ] Endpoint Admin/EB/TE có [Authorize(Roles=...)] đúng, không chỉ check ở FE
[ ] Không hardcode secret/connection string trong code mới
[ ] Background job có cơ chế chống chạy trùng (idempotent/lock)
```

---

## 6. Phần Bổ Sung — Các Gap Còn Thiếu

> Bổ sung sau lần review thứ 2. Các mục này dễ bị AI bỏ sót vì không liên quan trực tiếp đến lỗi "lưu token ở localStorage" ban đầu nhưng vẫn là rủi ro bảo mật/kiến trúc thật.

### 6.1 CSRF — bắt buộc vì đã chuyển sang cookie-based auth
> ⚠️ Đây là hệ quả trực tiếp của việc fix lỗi localStorage: chuyển token sang cookie giúp chống XSS đánh cắp token, nhưng lại mở nguy cơ CSRF nếu không xử lý thêm.

- ✅ Mọi endpoint có side-effect (POST/PUT/PATCH/DELETE) dùng cookie để xác thực phải có cơ chế chống CSRF — chọn 1 trong 2:
  - **Double-submit cookie token**: BE set thêm 1 cookie không-httpOnly chứa CSRF token, FE đọc và gửi lại qua header `X-CSRF-Token`, BE so khớp.
  - Hoặc kiểm tra `Origin`/`Referer` header khớp với domain FE cho phép.
- ✅ `SameSite=Lax` là tối thiểu cho cookie refreshToken (đã có ở mục 2.1); nếu FE và BE khác domain hoàn toàn (cross-site), bắt buộc thêm CSRF token, không dựa vào SameSite một mình.
- **Người phụ trách:** Nam (chủ Identity module) thiết kế cơ chế chung, áp dụng cho mọi Controller có cookie auth.

### 6.2 CORS — chưa được định nghĩa rõ
- ✅ BE chỉ định danh sách `AllowedOrigins` cụ thể (domain FE thật), **không dùng `AllowAnyOrigin()`**.
- ✅ Bắt buộc kèm `AllowCredentials()` vì FE gửi cookie — lưu ý: ASP.NET Core không cho phép `AllowAnyOrigin()` + `AllowCredentials()` cùng lúc, nên phải khai origin tường minh.
- ✅ Môi trường Dev/Staging/Production nên có danh sách origin riêng (đọc từ config, không hardcode).
- **Người phụ trách:** Bao (Core Phần 3 — Global Infrastructure), khai báo 1 lần trong `SharedInfrastructureExtensions.cs`, báo nhóm trước khi sửa.

### 6.3 Password Hashing & JWT Signing
- ✅ Hash password bằng **bcrypt** hoặc **Argon2** (ASP.NET Core Identity mặc định dùng PBKDF2 cũng chấp nhận được) — tuyệt đối không tự viết hàm hash hoặc dùng MD5/SHA1 trực tiếp.
- ✅ Salt phải random theo từng user (các thư viện chuẩn đã tự làm việc này — không cần tự cài).
- ✅ Ký JWT bằng **RS256** (asymmetric) thay vì HS256 nếu sau này có service khác (vd FastAPI SAM service) cần tự verify token mà không cần biết secret ký — RS256 chỉ cần share public key, an toàn hơn khi mở rộng ra nhiều service.
- ✅ JWT payload không chứa thông tin nhạy cảm (password, email cá nhân không cần thiết) vì payload chỉ encode chứ không mã hóa, ai cũng decode được.
- **Người phụ trách:** Nam.

### 6.4 File Upload — kiểm tra nội dung, không chỉ kiểm tra phần mở rộng
- ✅ Không tin `Content-Type` hoặc đuôi file (`.png`) do client gửi — kiểm tra **magic bytes** thực tế của file ở BE trước khi cho upload.
- ✅ File raw người dùng upload (bản thảo, layer vẽ) nên đi qua hàng đợi xử lý (resize/convert/transcode) trước khi cho phép truy cập công khai — không serve trực tiếp file gốc chưa qua xử lý.
- ✅ Vì hệ thống có content moderation pipeline (SAM/YOLO/U-Net), nên tận dụng luôn: file upload (đặc biệt ảnh bìa, trang chapter) đi qua bước scan nội dung trước khi publish, không chỉ dùng cho mục đích CV nghiệp vụ.
- **Người phụ trách:** Bao (S3/File Storage).

### 6.5 Bảo mật cho Python/FastAPI SAM Service (Google Colab)
> Đây là điểm đặc thù của MangaERP — service chạy trên Colab thường expose qua URL tạm (ngrok/cloudflare tunnel), rất dễ bị gọi trái phép nếu ai đó biết URL.

- ❌ Không để endpoint FastAPI trên Colab public hoàn toàn không xác thực.
- ✅ Dùng shared secret/API key cố định giữa .NET backend và FastAPI service — .NET gửi kèm header `X-Internal-Api-Key`, FastAPI kiểm tra trước khi xử lý.
- ✅ Giới hạn FastAPI chỉ nhận request từ IP/origin của BE nếu tunnel cho phép cấu hình (tùy giải pháp ngrok/cloudflare).
- ✅ Không log raw ảnh người dùng upload ở phía Colab notebook nếu không cần thiết — dễ rò rỉ khi notebook share nhầm.
- ✅ Vì Colab session có thể chết bất kỳ lúc nào → .NET backend phải có retry + timeout + fallback báo lỗi rõ ràng cho FE, không để request treo vô thời hạn.
- **Người phụ trách:** Bao/Nam tùy ai đang giữ phần CV integration — cần chốt trong team.

### 6.6 CSP & XSS cho Frontend
- ✅ Thêm header `Content-Security-Policy` ở BE (hoặc cấu hình ở reverse proxy/CDN) giới hạn nguồn script/style được load.
- ✅ Mọi nội dung do user nhập và hiển thị lại (feedback QA pin, comment vote, mô tả series) phải qua sanitize trước khi render — nếu dùng React, mặc định JSX đã escape, nhưng **tuyệt đối tránh `dangerouslySetInnerHTML`** trừ khi đã sanitize bằng thư viện như DOMPurify.
- ✅ Không render trực tiếp HTML/markdown từ user mà không qua sanitizer nếu sau này có tính năng rich text.

### 6.7 Quản Lý Secrets Trong Team
- ✅ Mỗi secret (JWT signing key, S3 access key, Colab API key, connection string Production) chỉ định danh người giữ bản gốc — đề xuất: Bao giữ secret hạ tầng (S3, Production DB), Nam giữ secret Identity/JWT.
- ❌ Không gửi secret qua chat nhóm dạng plaintext lâu dài (gửi xong nên xóa tin nhắn hoặc dùng kênh chia sẻ tạm thời).
- ✅ Dùng file `.env.example` (commit được, không chứa giá trị thật) để mọi người biết cần khai báo biến gì, file `.env` thật thì gitignore.
- ✅ Secret Production khác Secret Dev/Staging — không bao giờ dùng chung 1 JWT secret cho cả 2 môi trường.

### 6.8 Logging & PII (Personally Identifiable Information)
- ❌ Middleware log request/response không được log nguyên header `Authorization` hoặc `Cookie` (dễ vô tình log token).
- ✅ Nếu cần log để debug, chỉ log phần đã mask (vd `Bearer eyJ***`), không log full token.
- ✅ Audit Log (mục 1.3) không lưu email/IP nếu nghiệp vụ không thật sự cần truy vết — nếu cần, nên có chính sách retention (tự xóa log cũ sau X tháng) thay vì lưu vĩnh viễn.
- **Người phụ trách:** Bao (Audit Log, Exception Handler), áp dụng chung cho mọi middleware log của team.

### 6.9 Checklist bổ sung (nối thêm vào mục 5)

```
[ ] Endpoint POST/PUT/PATCH/DELETE dùng cookie auth có chống CSRF (token hoặc check Origin)
[ ] CORS chỉ định origin cụ thể, không dùng AllowAnyOrigin() khi có AllowCredentials()
[ ] Password hash bằng bcrypt/Argon2/PBKDF2, không tự chế thuật toán
[ ] File upload kiểm tra magic bytes, không chỉ tin Content-Type/đuôi file
[ ] Endpoint FastAPI/Colab có xác thực bằng API key riêng, không public open
[ ] Không dùng dangerouslySetInnerHTML khi chưa sanitize
[ ] Secret Production khác Secret Dev, không commit .env thật
[ ] Middleware log không log nguyên Authorization/Cookie header
[ ] Concurrency: lock 1 row → FOR UPDATE; lock aggregate nhiều rows → Serializable isolation
[ ] SignalR Hub đã test fallback transport (SSE/long-polling), không chỉ test WebSocket
[ ] List endpoint có giới hạn pageSize tối đa, không trả về toàn bộ DB
[ ] DELETE endpoint đã chốt rõ soft-delete hay hard-delete, query tự filter DeletedAt
[ ] GET /health trả JSON, không leak thông tin hạ tầng
```

---

## 7. Kỹ Thuật Bổ Sung — Xác Nhận Từ Code Review

> Bổ sung sau khi đối chiếu code thực tế (CastVoteHandler, ApproveSubmissionHandler) và gap_completion_plan.

### 7.1 Health Check (`GET /health`)
- ✅ Endpoint **public**, không `[Authorize]` — nhưng response **không lộ chi tiết hạ tầng nhạy cảm** (không trả connection string, không trả version nội bộ chi tiết, không trả tên server).
- ✅ Check tối thiểu 3 thành phần: DB connection, S3 (nếu khả dụng), SignalR Hub đang chạy. Có thể dùng `Microsoft.Extensions.Diagnostics.HealthChecks` (`AddDbContextCheck`, custom `IHealthCheck` cho S3).
- ✅ Response format chuẩn (tối giản):
  ```json
  { "status": "Healthy", "checks": { "database": "Healthy", "storage": "Healthy", "signalr": "Healthy" } }
  ```
- ❌ Không trả `Degraded`/`Unhealthy` kèm exception message chi tiết ra response public — chi tiết lỗi chỉ log nội bộ, response ngoài chỉ trả status đơn giản.
- **Người phụ trách:** Bach (Core Phần 2).

### 7.2 Concurrency / Transaction Isolation cho Vote & Conflict
> Xác nhận dựa trên code thực tế trong codebase — 2 pattern khác nhau, áp dụng tùy tình huống, không dùng 1 cách cho mọi handler.

- ✅ **Khi lock 1 row cụ thể** (vd: vote cho 1 submission, accept/reject 1 layer): dùng `SELECT ... FOR UPDATE` qua method dạng `GetByIdForUpdateAsync(id, ct)`, **không cần** khai báo `IsolationLevel.Serializable` — row-level lock đã đủ serialize access cho đúng row đó và hiệu quả hơn (không block toàn table).
  - Áp dụng cho: `CastVoteHandler`, các handler accept/reject 1 entity đơn lẻ.
- ✅ **Khi đọc aggregate qua nhiều row để ra quyết định** (vd: tính load của tất cả Tantou Editor để chọn người ít việc nhất rồi gán): dùng `await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)` để chống **phantom read** — nếu không, 2 request concurrent có thể cùng đọc thấy 1 TE có load thấp nhất và cùng gán vào, gây mất cân bằng tải.
  - Áp dụng cho: `ApproveSubmissionHandler` (load-balance gán Tantou Editor), và mọi handler tương tự dùng `COUNT`/aggregate query để ra quyết định ghi.
- ❌ Không mặc định dùng `Serializable` cho mọi handler — gây lock contention không cần thiết, làm chậm hệ thống khi nhiều request đồng thời.
- ✅ Quy tắc chọn nhanh: **lock được 1 row cụ thể bằng ID → `FOR UPDATE`. Phải đọc/quyết định dựa trên tập hợp nhiều row → `Serializable`.**
- **Người phụ trách:** Bao (MF1 — CastVoteHandler, ApproveSubmissionHandler, ResolveConflictHandler).

### 7.3 Pagination Policy cho List Endpoints
- ✅ **Bắt buộc** pagination cho mọi `GET` trả về danh sách có thể tăng trưởng theo thời gian (notifications, submissions queue, ranking board, chapters, tasks...). Ngoại lệ: danh sách cố định nhỏ (vd dropdown roles, genres tĩnh) không cần.
- ✅ Chuẩn dùng chung: **offset-based** `?page=1&pageSize=20` cho hầu hết list endpoint (đơn giản, đủ dùng cho UI có số trang). Dùng **cursor-based** (`?cursor=...`) riêng cho feed dạng infinite-scroll thật sự cần (vd Notifications nếu sau này làm dạng scroll liên tục) — không bắt buộc nếu UI hiện tại chỉ cần phân trang số.
- ✅ Giới hạn `pageSize` tối đa ở BE (vd `Math.Min(requestedSize, 100)`), không tin số FE gửi lên — tránh AI gen FE code lỡ gọi `pageSize=999999`.
- ✅ Response format chuẩn:
  ```json
  { "items": [...], "page": 1, "pageSize": 20, "totalCount": 134, "totalPages": 7 }
  ```
- **Người phụ trách:** Áp dụng chung — mỗi người tự enforce trong module mình, Bao chốt format chuẩn trong tài liệu API convention chung.

### 7.4 Soft Delete vs Hard Delete Policy
- ✅ **Soft delete** (field `DeletedAt`/`IsDeleted`) áp dụng cho entity có giá trị lịch sử/audit hoặc có thể cần khôi phục: `Submission`, `Chapter`, `Series`, `QA Pin`.
- ✅ **Hard delete** chấp nhận được cho entity không có giá trị truy vết: `Notification` (sau khi user xóa, không cần giữ lại), `Studio Invitation` đã cancel.
- ✅ Nếu dùng soft delete, **bắt buộc** mọi Query/Handler đọc danh sách phải tự filter `WHERE DeletedAt IS NULL` — cách an toàn nhất là dùng EF Core **Global Query Filter** (`modelBuilder.Entity<T>().HasQueryFilter(e => e.DeletedAt == null)`) khai báo 1 lần trong `AppDbContext`, để AI gen Handler mới không cần tự nhớ filter thủ công mỗi lần viết Query.
- ❌ Không để 2 kiểu xóa lẫn lộn trong cùng 1 entity (lúc thì set `DeletedAt`, lúc thì gọi `Remove()` thẳng) — phải nhất quán theo bảng phân loại trên.
- **Người phụ trách:** Người sở hữu module entity đó tự áp dụng đúng (Bao cho Submission/Series, Nam cho Chapter/Task, Bach cho QA Pin); Bao thêm Global Query Filter chung vào `AppDbContext.cs` (báo nhóm trước khi sửa theo luật mục 0.4).

### 7.5 Cross-Module Event Flow cho Notifications
> Giải quyết câu hỏi: làm sao Nam (Identity/Notifications) nhận được sự kiện từ Bao (MF1) hay Bach (MF3) mà không inject chéo DbContext/Repository giữa các module.

- ✅ Dùng **MediatR `INotification`** (domain event nội bộ trong cùng process) — đây là cách phù hợp nhất với quy mô team hiện tại, không cần message bus (RabbitMQ/Kafka) vì là monolith CQRS/MediatR sẵn có.
- ✅ Luồng chuẩn:
  1. Module nguồn (vd MF1 — `ApproveSubmissionHandler`) sau khi `SaveChangesAsync` thành công, publish 1 event: `await _mediator.Publish(new SubmissionApprovedEvent(submissionId, mangakaId), ct)`.
  2. Module Notifications (Nam) định nghĩa `Handler` riêng implement `INotificationHandler<SubmissionApprovedEvent>`, lắng nghe event này và tự tạo Notification + gửi qua SignalR — module Notifications **tự chứa logic của nó**, module MF1 không cần biết Notification được tạo thế nào.
- ✅ Event class (`SubmissionApprovedEvent`, `LayerRejectedEvent`...) đặt ở **Shared Kernel / Contracts layer** dùng chung — không đặt trong namespace riêng của module nguồn, để module đích (Notifications) reference được mà không phải reference toàn bộ module nguồn.
- ❌ Không gọi thẳng `INotificationRepository`/`NotificationService` từ Handler của module khác (vd không gọi `_notificationRepo.Add(...)` ngay trong `ApproveSubmissionHandler`) — đây chính là kiểu inject chéo cần tránh theo luật mục 0.3.
- ⚠️ Lưu ý: MediatR `Publish` mặc định chạy đồng bộ trong cùng transaction/process — nếu Handler phụ (gửi notification) lỗi, cần tự catch để không làm rollback toàn bộ transaction chính (vd action approve submission vẫn phải thành công dù gửi notification thất bại, chỉ log lỗi riêng).
- **Người phụ trách:** Nam định nghĩa interface/event contract chung; Bao, Bach publish event từ module của mình theo đúng tên event đã thống nhất.

### 7.6 SignalR-specific CORS
> Xác nhận: phân tích bổ sung là đúng và đầy đủ hơn — không chỉ là thêm `.AllowAnyHeader()`, mà vấn đề cốt lõi là transport fallback.

- ✅ SignalR vẫn cần `.AllowCredentials()` + khai origin FE cụ thể, giống CORS thường (mục 6.2) — không dùng `AllowAnyOrigin()`.
- ⚠️ **Đặc thù transport fallback:** Khi WebSocket bị proxy/firewall production chặn, SignalR tự động fallback xuống Server-Sent Events rồi long-polling — cả hai loại này đều đi qua CORS của browser (khác với WebSocket connection thường bypass CORS check kiểu preflight). Hệ quả thường gặp: **chạy tốt ở Dev (WebSocket trực tiếp) nhưng fail ở Production sau reverse proxy/Nginx** (rớt xuống long-polling và bị CORS chặn vì policy chỉ test với WebSocket).
- ✅ Thêm `.AllowAnyHeader()` trong CORS policy cho domain FE — theo khuyến nghị chính thức của Microsoft cho SignalR vì quá trình negotiate dùng nhiều header tùy transport.
- ✅ **Bắt buộc test cả 3 transport** (WebSocket, Server-Sent Events, Long Polling) trước khi merge code Hub, không chỉ test trên local (thường chỉ chạy WebSocket).
- **Người phụ trách:** Nam (chủ SignalR Hub) — thêm vào checklist PR riêng cho Hub: "đã test đủ 3 transport chưa".

### 7.7 Checklist bổ sung lần 2 (nối thêm vào mục 5 và 6.9)

```
[ ] GET /health không [Authorize], nhưng response không lộ chi tiết lỗi/hạ tầng
[ ] Handler lock 1 row cụ thể → dùng FOR UPDATE, không dùng Serializable thừa
[ ] Handler đọc aggregate nhiều row để quyết định ghi → dùng IsolationLevel.Serializable
[ ] Mọi GET list endpoint có pagination, giới hạn pageSize tối đa ở BE
[ ] Entity có Global Query Filter cho DeletedAt nếu dùng soft delete
[ ] Không gọi thẳng Repository/Service module khác — publish MediatR INotification event
[ ] SignalR Hub đã test đủ 3 transport (WebSocket/SSE/Long-polling) trước khi merge
```
