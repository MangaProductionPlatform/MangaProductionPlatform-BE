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
        <div style="background-color: #f9f9f9; padding: 30px; font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; min-height: 100%;">
            <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.05);">
                
                <div style="background: linear-gradient(135deg, #ff4e50, #f9d423); padding: 30px; text-align: center;">
                    <h1 style="color: #ffffff; margin: 0; font-size: 28px; letter-spacing: 1px; font-weight: bold; text-shadow: 1px 1px 2px rgba(0,0,0,0.2);">
                        MangaC&P
                    </h1>
                    <p style="color: #ffffff; margin: 5px 0 0 0; opacity: 0.9; font-size: 14px;">Hệ thống Sáng tác & Xuất bản Manga</p>
                </div>

                <div style="padding: 40px 30px; color: #333333; line-height: 1.6;">
                    <h2 style="margin-top: 0; color: #222222; font-size: 20px;">Chào {fullName},</h2>
                    <p style="font-size: 15px;">Chúc mừng bạn đã gia nhập cộng đồng tác giả và dịch giả của <strong>MangaC&P</strong>! Tài khoản hệ thống của bạn đã được khởi tạo thành công.</p>
                    
                    <div style="background-color: #f5f5f5; border-left: 4px solid #ff4e50; padding: 15px; margin: 20px 0; border-radius: 0 4px 4px 0;">
                        <p style="margin: 0; font-size: 14px; color: #555555;">Tên đăng nhập (Username) của bạn là:</p>
                        <p style="margin: 5px 0 0 0; font-size: 16px; font-weight: bold; color: #ff4e50;">{username}</p>
                    </div>

                    <p style="font-size: 15px; margin-bottom: 25px;">Vui lòng nhấn vào nút "Kích hoạt tài khoản" bên dưới để tiến hành thiết lập mật khẩu mới và bắt đầu hành trình sáng tác nhé:</p>
                    
                    <div style="text-align: center; margin: 30px 0;">
                        <a href="{activationLink}" target="_blank" style="background-color: #ff4e50; color: #ffffff; text-decoration: none; padding: 14px 35px; font-weight: bold; border-radius: 25px; font-size: 16px; display: inline-block; box-shadow: 0 4px 6px rgba(255,78,80,0.3); transition: all 0.2s;">
                            Kích Hoạt Tài Khoản
                        </a>
                    </div>

                    <p style="font-size: 13px; color: #888888; font-style: italic; text-align: center; margin-top: 30px;">
                        Lưu ý: Liên kết này có giá trị trong vòng 24 giờ. Nếu nút bấm trên không hoạt động, bạn có thể copy link sau dán vào trình duyệt: <br>
                        <a href="{activationLink}" style="color: #ff4e50; word-break: break-all;">{activationLink}</a>
                    </p>
                </div>

                <div style="background-color: #f1f1f1; padding: 20px; text-align: center; font-size: 12px; color: #999999; border-top: 1px solid #eaeaea;">
                    <p style="margin: 0 0 5px 0;">Đây là email tự động từ hệ thống MangaC&P, vui lòng không phản hồi thư này.</p>
                    <p style="margin: 0;">© 2026 MangaC&P. All rights reserved.</p>
                </div>

            </div>
        </div>
        """;
}
