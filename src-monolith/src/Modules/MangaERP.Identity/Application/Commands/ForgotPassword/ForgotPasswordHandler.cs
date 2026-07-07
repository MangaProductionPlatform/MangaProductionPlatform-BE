using System.Security.Cryptography;
using System.Text;
using MangaERP.Identity.Application.Ports;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MangaERP.Identity.Application.Commands.ForgotPassword;

// ── Models & DTOs ─────────────────────────────────────────────────────────────
public class OtpCacheEntry
{
    public string OtpHash { get; set; } = default!;
    public int FailedAttempts { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public record ForgotPasswordCommand(string Username) : IRequest<ForgotPasswordResult>;

public record ForgotPasswordResult(string Message, string MaskedEmail);

// ── Handler ───────────────────────────────────────────────────────────────────
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    private readonly IUserRepository _userRepo;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public const string CacheKeyPrefix = "otp:forgot-password:";
    public const int OtpExpiryMinutes = 5;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepo,
        IMemoryCache cache,
        IEmailService emailService,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _userRepo = userRepo;
        _cache = cache;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ForgotPasswordResult> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await _userRepo.GetByUsernameAsync(request.Username, ct);

        // Security best practice: User enumeration protection.
        // Always generate a masked email and return a successful response even if user doesn't exist.
        var targetEmail = user?.PersonalEmail ?? GenerateFakeEmailForMasking(request.Username);
        var maskedEmail = MaskEmail(targetEmail);

        if (user is null)
        {
            _logger.LogWarning("Forgot-password requested for non-existent username: {Username}", request.Username);
            return new ForgotPasswordResult("Mã OTP đã được gửi đến email cá nhân của bạn.", maskedEmail);
        }

        // Generate OTP and store SHA256 hash in MemoryCache
        var otp = GenerateOtp();
        var cacheEntry = new OtpCacheEntry
        {
            OtpHash = HashOtp(otp),
            FailedAttempts = 0,
            CreatedAtUtc = DateTime.UtcNow
        };

        _cache.Set(
            $"{CacheKeyPrefix}{request.Username}",
            cacheEntry,
            TimeSpan.FromMinutes(OtpExpiryMinutes));

        // Send OTP directly to their PersonalEmail
        await _emailService.SendOtpEmailAsync(user.PersonalEmail!, otp, ct);

        _logger.LogInformation("OTP generated and sent to PersonalEmail for user {Username}", request.Username);

        return new ForgotPasswordResult("Mã OTP đã được gửi đến email cá nhân của bạn.", maskedEmail);
    }

    private static string GenerateOtp()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }

    private static string MaskEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1) return "***@***.com";
        
        var namePart = email[..atIndex];
        var domainPart = email[atIndex..];

        if (namePart.Length <= 2)
            return $"{namePart[0]}***{domainPart}";
            
        return $"{namePart[0]}***{namePart[^1]}{domainPart}";
    }

    private static string GenerateFakeEmailForMasking(string username)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(username)))[..6].ToLower();
        return $"{hash}@gmail.com";
    }
}
