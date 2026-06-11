using System.Text;
using MangaERP.Identity;
using MangaERP.Submission;
using MangaERP.Series;
using MangaERP.Shared.Infrastructure;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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
            .AllowAnyMethod();
    });
});

// ── Shared Infrastructure (AppDbContext) ──────────────────────────────────────
builder.Services.AddSharedInfrastructure(builder.Configuration);

// ── Module Registration ────────────────────────────────────────────────────────
builder.Services.AddIdentityModule();
builder.Services.AddSubmissionModule();
builder.Services.AddSeriesModule();
// builder.Services.AddChapterModule();
// builder.Services.AddTaskModule();
// builder.Services.AddQaModule();
// builder.Services.AddPublishingModule();
// builder.Services.AddRankingModule();

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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
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
        await db.Database.MigrateAsync();
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
// Swagger available in Development only (or allow via env var for demo)
if (app.Environment.IsDevelopment() ||
    Environment.GetEnvironmentVariable("ENABLE_SWAGGER") == "true")
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MangaERP v1"));
}

app.UseCors(CorsPolicyName);

// Skip HTTPS redirect in Railway (Railway handles TLS at the gateway level)
if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
