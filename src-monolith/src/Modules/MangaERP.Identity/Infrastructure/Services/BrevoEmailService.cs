using MangaERP.Identity.Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace MangaERP.Identity.Infrastructure.Services;

/// <summary>
/// Email service using Brevo Transactional Email REST API (HTTPS port 443).
/// Unlike SMTP port 587, this is NEVER blocked by cloud providers like Render.
///
/// Production: Set Brevo__ApiKey environment variable.
/// Local Dev:  If API key is absent, falls back to logging the link to console.
///
/// How to get a free Brevo API key:
///   1. Register at https://app.brevo.com
///   2. Settings → SMTP & API → API Keys → Create a new API key
///   3. Add sender domain/email in "Senders & Domains" section
/// </summary>
public class BrevoEmailService : IEmailService
{
    private const string BrevoApiUrl = "https://api.brevo.com/v3/smtp/email";

    private readonly IConfiguration _config;
    private readonly ILogger<BrevoEmailService> _logger;
    private readonly HttpClient _httpClient;

    public BrevoEmailService(
        IConfiguration config,
        ILogger<BrevoEmailService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Brevo");
    }

    public async Task SendInvitationEmailAsync(
        string toEmail, string activationLink, string username,
        string fullName, CancellationToken ct = default)
    {
        var apiKey     = _config["Brevo:ApiKey"];
        var fromEmail  = _config["Smtp:FromAddress"] ?? "noreply@company.com";
        var fromName   = _config["Smtp:FromName"]   ?? "MangaERP";

        // ── Local Dev: No API key configured → log to console ─────────────────
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Brevo:ApiKey is not set. Falling back to console logging.");
            LogFallback(username, fullName, activationLink);
            return;
        }

        // ── Production: Call Brevo REST API ───────────────────────────────────
        try
        {
            var payload = new
            {
                sender      = new { name = fromName, email = fromEmail },
                to          = new[] { new { email = toEmail, name = fullName } },
                subject     = "🎉 Kích hoạt tài khoản MangaERP của bạn",
                htmlContent = BuildEmailHtml(fullName, username, activationLink),
                textContent = $"Xin chào {fullName},\n\nUsername: {username}\n\nKích hoạt tại: {activationLink}\n\nLink hết hạn sau 24 giờ."
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BrevoApiUrl);
            request.Headers.Add("api-key", apiKey);
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Brevo] Invitation email sent to {Email}", toEmail);
                return;
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("[Brevo] API error {Status}: {Body}", response.StatusCode, errorBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Brevo] Failed to send invitation email. Falling back to console logging.");
        }

        // ── Fallback: Log link if Brevo call failed ────────────────────────────
        LogFallback(username, fullName, activationLink);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void LogFallback(string username, string fullName, string activationLink)
    {
        _logger.LogWarning("********************************************************************************");
        _logger.LogWarning("ACTIVATION LINK FALLBACK:");
        _logger.LogWarning("User: {Username} ({FullName})", username, fullName);
        _logger.LogWarning("Activation Link: {Link}", activationLink);
        _logger.LogWarning("********************************************************************************");
    }

    private static string BuildEmailHtml(string fullName, string username, string activationLink)
        => $"""
        <!DOCTYPE html>
        <html lang="vi">
        <head><meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Kích hoạt tài khoản MangaERP</title></head>
        <body style="font-family:'Segoe UI',Arial,sans-serif;background:#f4f6f9;margin:0;padding:40px 0;">
          <div style="max-width:600px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,.08);">
            <div style="background:linear-gradient(135deg,#6366f1,#8b5cf6);padding:40px 32px;text-align:center;">
              <h1 style="color:#fff;margin:0;font-size:28px;letter-spacing:-0.5px;">🎌 MangaERP</h1>
              <p style="color:rgba(255,255,255,.85);margin:8px 0 0;font-size:14px;">Nền tảng quản lý sản xuất manga</p>
            </div>
            <div style="padding:40px 32px;">
              <h2 style="color:#1e1b4b;margin-top:0;">Xin chào, {fullName}! 👋</h2>
              <p style="color:#4b5563;line-height:1.6;">
                Tài khoản doanh nghiệp của bạn đã được Admin tạo thành công trên hệ thống MangaERP.
              </p>
              <div style="background:#f8f7ff;border:1px solid #e5e7eb;border-radius:8px;padding:16px 20px;margin:24px 0;">
                <p style="margin:0 0 8px;color:#6b7280;font-size:13px;text-transform:uppercase;letter-spacing:.5px;">Tài khoản của bạn</p>
                <p style="margin:0;font-size:18px;font-weight:600;color:#1e1b4b;font-family:monospace;">{username}</p>
              </div>
              <p style="color:#4b5563;line-height:1.6;">
                Nhấp vào nút bên dưới để đặt mật khẩu và kích hoạt tài khoản. Link này có hiệu lực trong <strong>24 giờ</strong>.
              </p>
              <div style="text-align:center;margin:32px 0;">
                <a href="{activationLink}"
                   style="display:inline-block;background:linear-gradient(135deg,#6366f1,#8b5cf6);color:#fff;text-decoration:none;padding:14px 36px;border-radius:8px;font-weight:600;font-size:16px;letter-spacing:.3px;">
                  ✅ Kích hoạt tài khoản
                </a>
              </div>
              <p style="color:#9ca3af;font-size:13px;line-height:1.5;">
                Nếu bạn không yêu cầu tài khoản này, hãy bỏ qua email này.<br>
                Link sẽ tự động hết hạn sau 24 giờ.
              </p>
            </div>
            <div style="background:#f9fafb;padding:20px 32px;text-align:center;border-top:1px solid #e5e7eb;">
              <p style="color:#9ca3af;font-size:12px;margin:0;">© 2026 MangaERP. All rights reserved.</p>
            </div>
          </div>
        </body></html>
        """;
}
