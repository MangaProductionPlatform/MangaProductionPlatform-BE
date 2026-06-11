# 🚀 MangaERP — Deployment Guide

> **Áp dụng cho:** `src-monolith` — ASP.NET Core 9 Modular Monolith + SQL Server

---

## 📋 Checklist trước khi deploy

- [x] `docker-compose build` đã chạy thành công
- [ ] Code đã push lên GitHub repo
- [ ] Đã có tài khoản Render hoặc Railway
- [ ] Đã có SQL Server managed (Azure SQL hoặc Railway MSSQL)
- [ ] Đã có Gmail App Password (16 ký tự) để gửi email

> [!IMPORTANT]
> Cả Render lẫn Railway KHÔNG có SQL Server miễn phí tích hợp sẵn. Railway có thể deploy MSSQL riêng (~$5 credit/tháng). Render cần dùng Azure SQL Free Tier bên ngoài.

---

# HƯỚNG DẪN 1: DEPLOY LÊN RENDER

## Bước 1 — Push code lên GitHub

```bash
git add .
git commit -m "feat: add Series API endpoints + fix Dockerfile"
git push origin main
```

> [!WARNING]
> Kiểm tra `.gitignore` có dòng `.env` trước khi push. KHÔNG push file `.env` thật lên GitHub.

## Bước 2 — Tạo Web Service trên Render

1. Đăng nhập render.com → **New +** → **Web Service**
2. **Connect a repository** → chọn GitHub repo `MangaProductionPlatform-BE`
3. Điền thông tin:

| Trường | Giá trị |
|---|---|
| **Name** | `mangaerp-api` |
| **Region** | Singapore |
| **Branch** | `main` |
| **Root Directory** | `src-monolith` |
| **Runtime** | Docker |
| **Dockerfile Path** | `./Dockerfile` |
| **Instance Type** | Free (0.1 CPU, 512MB RAM) |

4. Render tự detect `Dockerfile` — KHÔNG cần điền Build Command hay Start Command.

## Bước 3 — Chuẩn bị Database (Azure SQL Free)

Render không cung cấp SQL Server. Dùng Azure SQL Database free tier.

1. Vào portal.azure.com → **Create SQL Database**
2. Chọn:
   - **Server**: Tạo mới → đặt tên `mangaerp-server`, chọn region **Southeast Asia**
   - **Authentication**: SQL authentication → đặt username/password
   - **Compute + Storage**: `Free (32MB, 100k DTUs/tháng)`
3. Sau khi tạo xong → vào **Connection strings** → copy chuỗi **ADO.NET**

Ví dụ chuỗi kết nối:
```
Server=tcp:mangaerp-server.database.windows.net,1433;Database=MangaProductionDB;User ID=sa_user;Password=YourPass!;Encrypt=True;TrustServerCertificate=False;
```

4. **Bật firewall:** Vào SQL Server → **Networking** → bật **Allow Azure services** = ON

## Bước 4 — Khai báo Environment Variables trên Render

Trên trang Web Service → tab **Environment** → **Add Environment Variable**

### Bắt buộc (crash nếu thiếu)

| Key | Value | Ghi chú |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `Server=tcp:...database.windows.net,1433;Database=MangaProductionDB;User ID=your_user;Password=YourPass!;Encrypt=True;TrustServerCertificate=False;` | Chuỗi kết nối Azure SQL |
| `Jwt__Key` | `MangaERP@SuperSecret#2026!Prod` | Tối thiểu 32 ký tự |
| `Seed__AdminPassword` | `Admin@Render2026!` | Mật khẩu admin lần đầu |

### Cấu hình hệ thống

| Key | Value | Ghi chú |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `ENABLE_SWAGGER` | `true` | Đổi `false` khi không muốn expose Swagger |
| `Jwt__Issuer` | `MangaERP` | Giữ nguyên |
| `Jwt__Audience` | `MangaERP.Clients` | Giữ nguyên |
| `Jwt__ExpiryMinutes` | `60` | |
| `Jwt__RefreshTokenExpiryDays` | `7` | |
| `Invitation__ExpiryHours` | `24` | |
| `Invitation__ActivationBaseUrl` | `https://your-frontend.vercel.app/activate` | URL frontend thật |
| `Cors__AllowedOrigins` | `https://your-frontend.vercel.app` | Domain FE, nhiều domain cách nhau bởi `,` |

### SMTP Gmail

| Key | Value | Ghi chú |
|---|---|---|
| `Smtp__Host` | `smtp.gmail.com` | |
| `Smtp__Port` | `587` | |
| `Smtp__Username` | `your_gmail@gmail.com` | Gmail của bạn |
| `Smtp__Password` | `xxxx xxxx xxxx xxxx` | App Password 16 ký tự |
| `Smtp__FromAddress` | `your_gmail@gmail.com` | |
| `Smtp__FromName` | `MangaC&P Official` | |

> [!TIP]
> Lấy Gmail App Password: myaccount.google.com/apppasswords — Bắt buộc bật Xác minh 2 bước trước.

## Bước 5 — Deploy

1. Nhấn **Create Web Service**
2. Render tự clone repo → build Docker image → deploy
3. Theo dõi log ở tab **Logs**

Log thành công:
```
[DbSeeder] Admin account seeded: sysadmin.adm@company.com
Now listening on: http://0.0.0.0:10000
Application started.
```

> [!NOTE]
> Render free tier inject `PORT=10000` vào container. App đọc biến `PORT` nên tự bind đúng.

## Bước 6 — Verify

Swagger UI: `https://mangaerp-api.onrender.com/swagger`

Test login:
```json
POST https://mangaerp-api.onrender.com/api/v1/auth/login
{
  "username": "sysadmin.adm@company.com",
  "password": "Admin@Render2026!"
}
```

## Lưu ý đặc biệt Render Free Tier

| Vấn đề | Giải pháp |
|---|---|
| **Spin down sau 15 phút không có request** | Dùng UptimeRobot ping `GET /swagger/v1/swagger.json` mỗi 10 phút |
| **Cold start 30-60 giây** | Bình thường với free tier |
| **Không có persistent disk** | App stateless nên OK. Upload file cần S3/Cloudinary |

---

---

# HƯỚNG DẪN 2: DEPLOY LÊN RAILWAY

## Bước 1 — Push code lên GitHub

```bash
git add .
git commit -m "feat: add Series API endpoints + fix Dockerfile"
git push origin main
```

## Bước 2 — Tạo Project trên Railway

1. Đăng nhập railway.app → **New Project**
2. Chọn **Deploy from GitHub repo** → Authorize GitHub → chọn repo `MangaProductionPlatform-BE`

## Bước 3 — Tạo SQL Server Service trên Railway

1. Trong Project → **+ New Service** → **Database** → tìm **MSSQL**
2. Railway tự deploy SQL Server container, generate credentials
3. Vào service MSSQL → tab **Variables** → note lại các giá trị:
   - `MSSQLSERVER_HOST`
   - `MSSQLSERVER_PORT`
   - `MSSQLSERVER_SA_PASSWORD`

Convert sang ADO.NET connection string:
```
# Ví dụ Railway MSSQL values:
#   HOST = monorail.proxy.rlwy.net
#   PORT = 12345
#   PASSWORD = passw0rd

# ADO.NET cho ASP.NET Core:
Server=monorail.proxy.rlwy.net,12345;Database=MangaProductionDB;User Id=sa;Password=passw0rd;TrustServerCertificate=true
```

## Bước 4 — Tạo Service API trên Railway

1. Trong Project → **+ New Service** → **GitHub Repo** → chọn repo
2. Railway tự detect `Dockerfile`
3. Vào service → tab **Settings**:

| Setting | Giá trị |
|---|---|
| **Root Directory** | `src-monolith` |
| **Build Command** | để trống (dùng Dockerfile) |
| **Start Command** | để trống (dùng ENTRYPOINT) |

## Bước 5 — Khai báo Environment Variables trên Railway

Vào service API → tab **Variables** → nhấn **RAW EDITOR** → paste toàn bộ block sau (thay `<...>` bằng giá trị thật):

```
ConnectionStrings__DefaultConnection=Server=<MSSQLSERVER_HOST>,<MSSQLSERVER_PORT>;Database=MangaProductionDB;User Id=sa;Password=<MSSQLSERVER_SA_PASSWORD>;TrustServerCertificate=true
Jwt__Key=<chuoi_bi_mat_toi_thieu_32_ky_tu>
Jwt__Issuer=MangaERP
Jwt__Audience=MangaERP.Clients
Jwt__ExpiryMinutes=60
Jwt__RefreshTokenExpiryDays=7
Invitation__ExpiryHours=24
Invitation__ActivationBaseUrl=https://<your-frontend>.vercel.app/activate
Seed__AdminPassword=<mat_khau_admin_manh>
Cors__AllowedOrigins=https://<your-frontend>.vercel.app
ENABLE_SWAGGER=true
ASPNETCORE_ENVIRONMENT=Production
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__Username=<gmail_cua_ban>
Smtp__Password=<app_password_16_ky_tu>
Smtp__FromAddress=<gmail_cua_ban>
Smtp__FromName=MangaC&P Official
```

### Bảng chi tiết — Railway

#### Bắt buộc

| Key | Value | Ghi chú |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | `Server=<host>,<port>;Database=MangaProductionDB;User Id=sa;Password=<pass>;TrustServerCertificate=true` | Copy từ Railway MSSQL Variables |
| `Jwt__Key` | `MangaERP@SuperSecret#2026!Railway` | Min 32 ký tự |
| `Seed__AdminPassword` | `Admin@Railway2026!` | Mật khẩu seed admin lần đầu |

#### Cấu hình hệ thống

| Key | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ENABLE_SWAGGER` | `true` |
| `Jwt__Issuer` | `MangaERP` |
| `Jwt__Audience` | `MangaERP.Clients` |
| `Jwt__ExpiryMinutes` | `60` |
| `Jwt__RefreshTokenExpiryDays` | `7` |
| `Invitation__ExpiryHours` | `24` |
| `Invitation__ActivationBaseUrl` | `https://your-fe.vercel.app/activate` |
| `Cors__AllowedOrigins` | `https://your-fe.vercel.app` |

#### SMTP Gmail

| Key | Value |
|---|---|
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__Username` | `your_gmail@gmail.com` |
| `Smtp__Password` | `xxxx xxxx xxxx xxxx` |
| `Smtp__FromAddress` | `your_gmail@gmail.com` |
| `Smtp__FromName` | `MangaC&P Official` |

> [!NOTE]
> Railway tự inject `PORT` vào container. KHÔNG cần khai báo `PORT` thủ công.

## Bước 6 — Deploy

1. Sau khi save biến → Railway tự trigger deploy
2. Theo dõi log: tab **Deployments** → **View Logs**
3. Deploy xong → Railway cấp URL: `https://mangaerp-api-production.up.railway.app`

Log thành công:
```
[DbSeeder] Admin account seeded: sysadmin.adm@company.com
Now listening on: http://0.0.0.0:XXXX
Application started. Press Ctrl+C to shut down.
```

## Bước 7 — Verify trên Railway

Swagger: `https://<your-service>.up.railway.app/swagger`

Test login:
```bash
curl -X POST https://<your-service>.up.railway.app/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"sysadmin.adm@company.com","password":"Admin@Railway2026!"}'
```

## Custom Domain trên Railway

1. Service → **Settings** → **Networking** → **Custom Domain**
2. Nhập domain → Railway cấp CNAME record
3. Vào DNS provider → thêm CNAME trỏ về Railway
4. SSL/TLS được cấp tự động (Let's Encrypt)

---

---

# So sánh Render vs Railway

| Tiêu chí | Render | Railway |
|---|---|---|
| **Free Tier** | 750h/tháng | $5 credit/tháng |
| **SQL Server có sẵn** | Không (cần Azure SQL ngoài) | Có (deploy trực tiếp) |
| **Spin down khi idle** | Có (sau 15 phút) | Không (luôn chạy) |
| **Cold start** | 30-60 giây | Không có |
| **Deploy từ Dockerfile** | Tự detect | Tự detect |
| **Custom domain + SSL** | Miễn phí | Miễn phí |
| **Region gần VN** | Singapore | Singapore |
| **Độ phức tạp setup** | Trung bình | Đơn giản |
| **Khuyến nghị** | Demo/Test nhanh | SWP demo chính |

> [!TIP]
> Khuyến nghị cho SWP: Dùng Railway — có sẵn MSSQL, không spin down, setup dễ hơn.
> Khi hết $5 credit thì chuyển sang Render + Azure SQL Free.

---

# Tóm tắt — Biến bắt buộc phải set

```
# CỐT LÕI — APP CRASH NẾU THIẾU
ConnectionStrings__DefaultConnection   ← chuỗi kết nối SQL Server
Jwt__Key                               ← secret key JWT (>= 32 ký tự)
Seed__AdminPassword                    ← mật khẩu admin lần đầu

# QUAN TRỌNG CHO PRODUCTION
Invitation__ActivationBaseUrl          ← URL frontend /activate
Cors__AllowedOrigins                   ← domain frontend

# EMAIL (cần để gửi invitation)
Smtp__Username                         ← Gmail của bạn
Smtp__Password                         ← App Password 16 ký tự
```
