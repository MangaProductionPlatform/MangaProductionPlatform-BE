using MangaERP.Identity.Application.Ports;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MangaERP.Identity.Infrastructure.Services;

/// <summary>
/// SMTP email service using MailKit.
/// Production: Brevo (smtp-relay.brevo.com:587) — set BREVO_SMTP_USER / BREVO_SMTP_PASS env vars.
/// Local Dev:  Maildev container (maildev:1025) — configured via docker-compose.yml.
/// If SMTP is unconfigured, falls back to logging the activation link to console.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly Microsoft.Extensions.Logging.ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IConfiguration config,
        Microsoft.Extensions.Logging.ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendInvitationEmailAsync(
        string toEmail, string activationLink, string username,
        string fullName, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        var fromAddress = _config["Smtp:FromAddress"] ?? "noreply@company.com";
        var fromName = _config["Smtp:FromName"] ?? "MangaERP";
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "🎉 Kích hoạt tài khoản MangaERP của bạn";

        var builder = new BodyBuilder
        {
            HtmlBody = BuildEmailHtml(fullName, username, activationLink),
            TextBody = $"Xin chào {fullName},\n\nTài khoản doanh nghiệp của bạn đã được tạo:\n" +
                       $"Username: {username}\n\nKích hoạt tại: {activationLink}\n\nLink hết hạn sau 24 giờ."
        };
        message.Body = builder.ToMessageBody();

        var host = _config["Smtp:Host"] ?? "smtp.gmail.com";
        var portStr = _config["Smtp:Port"] ?? "587";
        var usernameSmtp = _config["Smtp:Username"];
        var passwordSmtp = _config["Smtp:Password"];

        // Developer friendly check: if SMTP parameters are missing or default placeholders, log to console instead of failing
        if (string.IsNullOrWhiteSpace(usernameSmtp) || usernameSmtp.Contains("YOUR_GMAIL") || string.IsNullOrWhiteSpace(passwordSmtp))
        {
            _logger.LogWarning("********************************************************************************");
            _logger.LogWarning("DEVELOPER NOTICE: SMTP credentials are not configured or contain placeholders.");
            _logger.LogWarning("Email invitation not sent via network. Copy the link below to activate:");
            _logger.LogWarning("User: {Username} ({FullName})", username, fullName);
            _logger.LogWarning("Activation Link: {ActivationLink}", activationLink);
            _logger.LogWarning("********************************************************************************");
            return;
        }

        try
        {
            using var client = new SmtpClient();
            var port = int.Parse(portStr);
            
            // Auto options based on port
            var security = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            
            await client.ConnectAsync(host, port, security, ct);
            await client.AuthenticateAsync(usernameSmtp, passwordSmtp, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMTP email. Falling back to console logging.");
            _logger.LogWarning("********************************************************************************");
            _logger.LogWarning("ACTIVATION LINK FALLBACK:");
            _logger.LogWarning("User: {Username} ({FullName})", username, fullName);
            _logger.LogWarning("Activation Link: {ActivationLink}", activationLink);
            _logger.LogWarning("********************************************************************************");
        }
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
