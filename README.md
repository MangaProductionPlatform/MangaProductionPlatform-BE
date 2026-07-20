# Manga Production Platform

Hệ thống quản lý quy trình sản xuất manga, gồm:

- Backend: ASP.NET Core 9 modular monolith, Entity Framework Core và PostgreSQL.
- Frontend: React 19, TypeScript, Vite và Tailwind CSS.
- Dịch vụ hỗ trợ: MailDev (email local), Cloudinary (lưu ảnh) và SAM service (AI segmentation).

README này hướng dẫn setup toàn bộ hệ thống trên máy local từ đầu. Cách nhanh và ít lỗi nhất là chạy backend, PostgreSQL và MailDev bằng Docker; frontend chạy bằng Node.js.

## 1. Yêu cầu hệ thống

Cài các công cụ sau:

| Công cụ | Phiên bản đề nghị | Dùng cho |
|---|---:|---|
| Git | Bản ổn định mới | Clone source code |
| Docker Desktop | Bản ổn định mới | API, PostgreSQL, MailDev |
| Node.js | `22.x` | Frontend Vite 8 |
| npm | `10.x` trở lên | Cài package frontend |
| .NET SDK | `9.0.x` | Chỉ cần nếu chạy backend trực tiếp |

Kiểm tra sau khi cài:

```bash
git --version
docker --version
docker compose version
node --version
npm --version
dotnet --version
```

Nếu máy chỉ nhận lệnh `docker-compose`, thay `docker compose` trong tài liệu bằng `docker-compose`.

## 2. Lấy source code

Backend và frontend là hai repository riêng. Nên đặt chúng cạnh nhau:

```text
MG_prjs/
├── MangaProductionPlatform-BE/
└── MangaProductionPlatform-FE/
```

```bash
mkdir MG_prjs
cd MG_prjs
git clone https://github.com/MangaProductionPlatform/MangaProductionPlatform-BE.git
git clone https://github.com/MangaProductionPlatform/MangaProductionPlatform-FE.git
```

## 3. Tạo tài khoản dịch vụ ngoài

### Cloudinary - bắt buộc

API cần đủ ba credential Cloudinary để khởi tạo chức năng lưu ảnh:

1. Tạo tài khoản tại Cloudinary.
2. Mở Dashboard của product environment.
3. Lấy `Cloud name`, `API key` và `API secret`.
4. Không commit các giá trị này lên Git.

### Brevo - tùy chọn khi chạy local

Ứng dụng dùng HTTP API của Brevo để gửi email. Nếu không khai báo API key, nội dung email/activation link được ghi ra log backend, vẫn phù hợp để phát triển local.

Để gửi email thật, tạo API key trong Brevo và xác thực địa chỉ gửi tương ứng với `SMTP_FROM_ADDRESS`.

### SAM service - tùy chọn

SAM phục vụ tính năng AI segmentation. Có thể để URL placeholder khi chỉ phát triển các module khác. Khi cần AI, cung cấp URL public của SAM service (ví dụ URL ngrok) và API key nội bộ nếu service yêu cầu.

## 4. Cấu hình backend ENV

Đi tới thư mục backend monolith và sao chép file mẫu:

### Windows PowerShell

```powershell
cd MangaProductionPlatform-BE\src-monolith
Copy-Item .env.example .env
```

### macOS/Linux

```bash
cd MangaProductionPlatform-BE/src-monolith
cp .env.example .env
```

Mở `.env` và điền tối thiểu các biến sau:

```env
POSTGRES_PASSWORD=change_this_database_password
JWT_KEY=change_this_to_a_random_secret_at_least_32_characters
ADMIN_PASSWORD=change_this_admin_password

ACTIVATION_BASE_URL=http://localhost:5173/activate
CORS_ORIGINS=http://localhost:5173

Cloudinary__CloudName=your_cloud_name
Cloudinary__ApiKey=your_api_key
Cloudinary__ApiSecret=your_api_secret
```

Không thêm dấu nháy quanh giá trị. Không để khoảng trắng hai bên dấu `=`. Với JWT secret, có thể tạo giá trị ngẫu nhiên bằng PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

### Danh sách biến backend

| Biến trong `.env` | Bắt buộc | Ý nghĩa |
|---|:---:|---|
| `POSTGRES_PASSWORD` | Có khi dùng Docker | Mật khẩu user `postgres` của database local |
| `JWT_KEY` | Có | Secret ký access token, tối thiểu 32 ký tự |
| `ADMIN_PASSWORD` | Có | Mật khẩu tài khoản admin được seed lần đầu |
| `ACTIVATION_BASE_URL` | Có | Trang frontend nhận link kích hoạt |
| `CORS_ORIGINS` | Có | Origin frontend được phép gọi API; nhiều origin cách nhau bằng dấu phẩy |
| `Cloudinary__CloudName` | Có | Cloud name của Cloudinary |
| `Cloudinary__ApiKey` | Có | API key của Cloudinary |
| `Cloudinary__ApiSecret` | Có | API secret của Cloudinary |
| `BREVO_API_KEY` | Không | API key gửi email thật; bỏ trống thì email được log ra console |
| `SMTP_FROM_ADDRESS` | Khi dùng Brevo | Địa chỉ người gửi đã được xác thực |
| `SMTP_FROM_NAME` | Không | Tên hiển thị của người gửi |
| `SamService__Url` | Khi dùng AI | Base URL của SAM service, không có dấu `/` cuối |
| `SamService__InternalApiKey` | Không | Shared secret giữa backend và SAM service |

File mẫu còn có nhóm biến dạng `ConnectionStrings__...`, `Jwt__...`. Nhóm này dùng khi chạy backend trực tiếp bằng `dotnet run`; dấu `__` được ASP.NET Core ánh xạ thành cấu hình lồng nhau.

## 5. Chạy backend bằng Docker - khuyến nghị

Từ `MangaProductionPlatform-BE/src-monolith`:

```bash
docker compose up --build -d
```

Docker sẽ chạy:

| Thành phần | Địa chỉ |
|---|---|
| Backend API | `http://localhost:8080` |
| Swagger UI | `http://localhost:8080/swagger` |
| PostgreSQL | `localhost:5432` |
| MailDev web UI | `http://localhost:1080` |
| MailDev SMTP | `localhost:1025` |

Theo dõi log lần khởi động đầu tiên:

```bash
docker compose logs -f mangaerp-api
```

API tự chạy EF Core migration và seed dữ liệu. Khi thấy ứng dụng đang lắng nghe ở cổng `8080`, nhấn `Ctrl+C` để thoát chế độ xem log; container vẫn tiếp tục chạy.

Kiểm tra trạng thái:

```bash
docker compose ps
curl http://localhost:8080/health
```

Trên PowerShell cũng có thể dùng:

```powershell
Invoke-RestMethod http://localhost:8080/health
```

### Các lệnh Docker thường dùng

```bash
# Dừng hệ thống, giữ nguyên dữ liệu PostgreSQL
docker compose down

# Khởi động lại
docker compose up -d

# Xem log toàn bộ service
docker compose logs -f

# Xóa cả container và dữ liệu database local
docker compose down -v
```

Lệnh cuối xóa volume database và không thể khôi phục dữ liệu local đã có.

## 6. Chạy frontend

Mở terminal thứ hai:

### Windows PowerShell

```powershell
cd MangaProductionPlatform-FE\MangaStudioPlatform_FE
Copy-Item .env.example .env.local
npm ci
npm run dev
```

### macOS/Linux

```bash
cd MangaProductionPlatform-FE/MangaStudioPlatform_FE
cp .env.example .env.local
npm ci
npm run dev
```

Nội dung local mặc định:

```env
VITE_API_BASE_URL=http://localhost:8080
```

Mở địa chỉ Vite in ra terminal, thông thường là `http://localhost:5173`.

Lưu ý: Vite đọc ENV tại thời điểm khởi động/build. Sau khi đổi `.env.local`, phải dừng và chạy lại `npm run dev`.

## 7. Đăng nhập và sử dụng

Tài khoản admin được backend seed ở lần chạy đầu:

```text
Username: sysadmin.adm@company.com
Password: giá trị ADMIN_PASSWORD trong backend/.env
```

Quy trình kiểm tra nhanh:

1. Mở `http://localhost:8080/health` và chắc chắn trạng thái là `Healthy`.
2. Mở `http://localhost:8080/swagger` để thử API trực tiếp.
3. Mở frontend ở `http://localhost:5173`.
4. Đăng nhập bằng tài khoản admin ở trên.
5. Với email local, mở MailDev tại `http://localhost:1080` để xem thư mời/kích hoạt.

Có thể test login bằng PowerShell:

```powershell
$body = @{
  username = 'sysadmin.adm@company.com'
  password = 'YOUR_ADMIN_PASSWORD'
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:8080/api/v1/auth/login `
  -ContentType 'application/json' `
  -Body $body
```

## 8. Chạy backend trực tiếp bằng .NET

Dùng cách này khi cần debug C#. PostgreSQL và MailDev vẫn có thể chạy bằng Docker:

```bash
cd MangaProductionPlatform-BE/src-monolith
docker compose up -d postgres maildev
dotnet restore
dotnet run --project src/MangaERP.Api/MangaERP.Api.csproj
```

Trong `.env`, điền nhóm cấu hình trực tiếp:

```env
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=MangaProductionDB;Username=postgres;Password=change_this_database_password
Jwt__Key=change_this_to_a_random_secret_at_least_32_characters
Seed__AdminPassword=change_this_admin_password
Invitation__ActivationBaseUrl=http://localhost:5173/activate
Cors__AllowedOrigins=http://localhost:5173
Brevo__ApiKey=
Smtp__FromAddress=noreply@example.com
Smtp__FromName=MangaERP
SamService__Url=https://placeholder.ngrok-free.app
SamService__InternalApiKey=
Cloudinary__CloudName=your_cloud_name
Cloudinary__ApiKey=your_api_key
Cloudinary__ApiSecret=your_api_secret
```

API bind cổng `8080` theo cấu hình trong `Program.cs`. Migration và seed chạy tự động khi khởi động.

## 9. Kiểm tra code trước khi commit

Backend:

```bash
cd MangaProductionPlatform-BE/src-monolith
dotnet build MangaERP.sln
dotnet test MangaERP.sln
```

Frontend:

```bash
cd MangaProductionPlatform-FE/MangaStudioPlatform_FE
npm run lint
npm run build
npm run preview
```

## 10. Xử lý lỗi thường gặp

### API dừng ngay khi khởi động

Kiểm tra log:

```bash
docker compose logs mangaerp-api
```

Các nguyên nhân thường gặp là thiếu `JWT_KEY`, `ADMIN_PASSWORD`, credential Cloudinary hoặc database chưa healthy. Kiểm tra `.env`, sau đó chạy lại:

```bash
docker compose up --build -d
```

### Frontend báo `Missing VITE_API_BASE_URL`

Tạo `MangaStudioPlatform_FE/.env.local`, khai báo `VITE_API_BASE_URL`, rồi khởi động lại Vite.

### Lỗi CORS

`CORS_ORIGINS` phải là origin, không có path và không có dấu `/` cuối:

```env
CORS_ORIGINS=http://localhost:5173,https://your-frontend.example.com
```

Sau khi đổi ENV backend, restart API:

```bash
docker compose up -d --force-recreate mangaerp-api
```

### Sai mật khẩu admin sau khi đổi ENV

Admin chỉ được seed khi chưa tồn tại. Đổi `ADMIN_PASSWORD` không cập nhật tài khoản đã có. Với database local không cần giữ dữ liệu, reset volume rồi khởi động lại:

```bash
docker compose down -v
docker compose up --build -d
```

### Cổng đã được sử dụng

Các cổng cần trống: `5173`, `8080`, `5432`, `1080`, `1025`. Dừng ứng dụng đang chiếm cổng hoặc đổi mapping tương ứng trong `docker-compose.yml`.

### Email không xuất hiện trong MailDev

Backend hiện ưu tiên Brevo HTTP API. Khi `BREVO_API_KEY` để trống, email được ghi vào log API; kiểm tra bằng:

```bash
docker compose logs -f mangaerp-api
```

## 11. Nguyên tắc bảo mật ENV

- Không commit `.env`, `.env.local`, API secret hoặc mật khẩu thật.
- Chỉ commit `.env.example` với placeholder.
- Dùng secret khác nhau cho local, staging và production.
- Khi nghi ngờ secret đã lộ, rotate JWT key, database password, Brevo key và Cloudinary secret.
- Trên môi trường deploy, khai báo biến trong dashboard của nền tảng thay vì upload file `.env`.