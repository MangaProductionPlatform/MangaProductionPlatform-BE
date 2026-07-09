using System.Text;
using MangaERP.Api.Services;
using DotNetEnv;
using MangaERP.Identity;
using MangaERP.Submission;
using MangaERP.Series;
using MangaERP.Studio;
using MangaERP.Chapter;
using MangaERP.Task;
using MangaERP.QA;
using MangaERP.Publishing;
using MangaERP.Segmentation;
using MangaERP.Ranking;
using MangaERP.Shared.Infrastructure;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MangaERP.Shared.Infrastructure.Hubs;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// ── Load .env for local development (ignored in Docker / Render / Railway) ────
// DotNetEnv tự động inject vào System.Environment → ASP.NET Core config sẽ đọc được
// Tìm file .env từ thư mục src-monolith (thư mục gốc của project)
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Đảm bảo ASP.NET Core đọc các biến đã được DotNetEnv inject vào System.Environment
// Biến dạng Smtp__Host trong .env sẽ được map tự động sang Smtp:Host trong config
builder.Configuration.AddEnvironmentVariables();

// ── Dynamic PORT (Railway / Docker) ───────────────────────────────────────────
// Railway injects $PORT at runtime. Kestrel listens on that port.
builder.WebHost.ConfigureKestrel(options =>
{
    var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "8080");
    options.ListenAnyIP(port);
});

const string CorsPolicyName = "FrontendCors";

var rawAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

if (rawAllowedOrigins != null && rawAllowedOrigins.Length == 1 && rawAllowedOrigins[0].Contains(","))
{
    rawAllowedOrigins = rawAllowedOrigins[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

var allowedOrigins = (rawAllowedOrigins == null || rawAllowedOrigins.Length == 0)
    ? new[]
    {
        "https://manga-production-platform-fe.vercel.app",
        "http://localhost:5173",
        "http://localhost:8080"
    }
    : rawAllowedOrigins;

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR WebSocket transport
    });
});

// ── Shared Infrastructure (AppDbContext) ──────────────────────────────────────
builder.Services.AddSharedInfrastructure(builder.Configuration);

// ── Module Registration ────────────────────────────────────────────────────────
builder.Services.AddIdentityModule();
builder.Services.AddSubmissionModule();
builder.Services.AddSeriesModule();
builder.Services.AddStudioModule();
builder.Services.AddChapterModule();
builder.Services.AddTaskModule();
builder.Services.AddSegmentationModule(builder.Configuration);
builder.Services.AddQaModule();
builder.Services.AddPublishingModule();
builder.Services.AddRankingModule();

// ── Api-level MediatR Handlers (cross-module queries) ────────────────────────
// GetAdminDashboardHandler cần inject từ nhiều module → đặt ở Composition Root (Api)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// ── JWT Authentication ─────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured. Set it via Railway environment variables.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer    = builder.Configuration["Jwt:Issuer"],
            ValidAudience  = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// ── Controllers ────────────────────────────────────────────────────────────────
// JsonStringEnumConverter: cho phép API nhận enum dưới dạng chuỗi ("Content", "Visual", "Typo")
// thay vì chỉ số nguyên (0, 1, 2). Không breaking với client dùng số nguyên.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSignalR();

// ── SAM (Segment Anything Model) Service ──────────────────────────────────────
// Typed HttpClient — base URL comes from "SamService:Url" in appsettings / env var.
// Override SAM URL via env: SamService__Url=https://your-ngrok-url.ngrok-free.app
var samUrl = builder.Configuration["SamService:Url"]
    ?? throw new InvalidOperationException("SamService:Url is not configured.");
builder.Services.AddHttpClient<MangaERP.Api.Services.SamServiceClient>(client =>
{
    client.BaseAddress = new Uri(samUrl);
    // vit_b cold start on Colab T4 (first embedding) can take ~2 min.
    // Predict calls are fast (~2-5s) but we use one shared timeout for simplicity.
    client.Timeout = TimeSpan.FromSeconds(180);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// ── Cloudinary Image Storage ──────────────────────────────────────────────────
// Credential config via .env: Cloudinary__CloudName, Cloudinary__ApiKey, Cloudinary__ApiSecret
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<MangaERP.Shared.Infrastructure.HealthChecks.DbHealthCheck>("database", tags: new[] { "db", "ready" })
    .AddCheck<MangaERP.Shared.Infrastructure.HealthChecks.CacheHealthCheck>("memory-cache", tags: new[] { "cache", "ready" });

// ── Rate Limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var payload = new
        {
            error = "TooManyRequests",
            message = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút."
        };
        await context.HttpContext.Response.WriteAsJsonAsync(payload, cancellationToken);
    };

    // Policy riêng cho các endpoint auth nhạy cảm (login, forgot-password, reset-password)
    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Policy chung cho toàn bộ API còn lại (baseline chống spam/DOS cơ bản)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(type => type.FullName);
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MangaERP API", Version = "v1",
        Description = "Manga Production Platform — Modular Monolith" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme { Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
        Array.Empty<string>()
    }});
});

var app = builder.Build();

// ── Database Migration + Seed ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    try
    {
        // [POSTGRESQL - Mặc định khi deploy] (Uncomment khi deploy)
        await db.Database.MigrateAsync();

        // [SQL SERVER / SSMS - Dùng test local]
        // await db.Database.EnsureCreatedAsync();
        // Seed admin on first run in both Development AND Production (Railway)
        // if no admin exists yet
        await DbSeeder.SeedAsync(db, config);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration/seed failed.");
        throw;
    }
}

// ── Middleware Pipeline ────────────────────────────────────────────────────────
// 1. Global exception middleware (must be earliest to catch errors in other middlewares)
app.UseMiddleware<MangaERP.Shared.Infrastructure.Middlewares.GlobalExceptionMiddleware>();

// 2. Rate limiter
app.UseRateLimiter();

// Swagger available in Development only (or allow via env var for demo)
if (app.Environment.IsDevelopment() ||
    Environment.GetEnvironmentVariable("ENABLE_SWAGGER") == "true")
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MangaERP v1"));
}

app.UseCors(CorsPolicyName);
// Static files for public uploads (local/demo only).
// Skipped when directory doesn't exist (e.g. fresh Docker container on Render).
// Production uploads go to Cloudinary — this block is a no-op in that case.
var publicUploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "public");
if (Directory.Exists(publicUploadsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(publicUploadsPath),
        RequestPath  = "/uploads/public"
    });
}

// Skip HTTPS redirect in Railway (Railway handles TLS at the gateway level)
if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();

app.UseAuthentication();

// 3. Token Blacklist middleware (runs after Authentication to access User claims, before Authorization)
app.UseMiddleware<MangaERP.Shared.Infrastructure.Middlewares.TokenBlacklistMiddleware>();

app.UseAuthorization();

// ── Health Checks Routing ──────────────────────────────────────────────────────
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds
            }),
            totalDurationMs = report.TotalDuration.TotalMilliseconds
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // liveness probe: returns 200 immediately without running DB checks
});

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.Run();

#pragma warning disable CA1050 // Declare types in namespaces
public partial class Program
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx, 408
            .WaitAndRetryAsync(2, i => TimeSpan.FromSeconds(2 * i),
                onRetry: (outcome, delay, attempt, ctx) =>
                    Console.WriteLine($"[SAM] Retry {attempt} sau {delay.TotalSeconds}s — lý do: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}"));

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 4, // 4 lần fail liên tiếp
                durationOfBreak: TimeSpan.FromMinutes(2), // tạm ngưng 2 phút rồi thử lại
                onBreak: (outcome, duration) =>
                    Console.WriteLine($"[SAM] Circuit OPEN — tạm ngưng gọi SAM {duration.TotalSeconds}s"),
                onReset: () => Console.WriteLine("[SAM] Circuit CLOSED — SAM service đã phục hồi"));
}
#pragma warning restore CA1050
