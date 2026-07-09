using System.Security.Cryptography;
using System.Text;
using MangaERP.Identity.Application.Commands.ForgotPassword;
using MangaERP.Identity.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MangaERP.Identity.Application.Commands.ResetPassword;

// ── Command ───────────────────────────────────────────────────────────────────
public record ResetPasswordCommand(
    string Username,
    string Otp,
    string NewPassword
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    private const int MaxFailedAttempts = 5;

    public ResetPasswordCommandHandler(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IMemoryCache cache,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var cacheKey = $"{ForgotPasswordCommandHandler.CacheKeyPrefix}{request.Username}";

        if (!_cache.TryGetValue(cacheKey, out OtpCacheEntry? entry) || entry is null)
        {
            throw new UnauthorizedAccessException("Mã OTP không hợp lệ hoặc đã hết hạn.");
        }

        // Prevent brute-force OTP attempts
        if (entry.FailedAttempts >= MaxFailedAttempts)
        {
            _cache.Remove(cacheKey);
            _logger.LogWarning("OTP locked out due to too many failed attempts for {Username}", request.Username);
            throw new UnauthorizedAccessException("Bạn đã nhập sai mã OTP quá nhiều lần. Vui lòng yêu cầu gửi lại mã OTP.");
        }

        // Validate input OTP using fixed time comparison to prevent timing attacks
        var inputHash = HashOtp(request.Otp);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(inputHash),
                Encoding.UTF8.GetBytes(entry.OtpHash)))
        {
            entry.FailedAttempts++;
            // Re-store in cache with updated attempt count
            _cache.Set(cacheKey, entry, TimeSpan.FromMinutes(ForgotPasswordCommandHandler.OtpExpiryMinutes));
            throw new ArgumentException("Mã OTP không chính xác.");
        }

        // Enforce password strength policies
        ValidatePasswordPolicy(request.NewPassword);

        var user = await _userRepo.GetByUsernameAsync(request.Username, ct)
            ?? throw new EntityNotFoundException("User", Guid.Empty); // Username doesn't exist, though OTP was somehow set

        // Hash and update the password using BCrypt
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepo.UpdateAsync(user, ct);

        // Security best practice: Revoke all active refresh tokens/sessions on password reset
        await _refreshTokenRepo.RevokeAllForUserAsync(user.Id, ct);

        // Remove OTP cache entry
        _cache.Remove(cacheKey);

        _logger.LogInformation("Password reset successful for user {Username}. All sessions revoked.", request.Username);
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }

    private static void ValidatePasswordPolicy(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Mật khẩu phải có ít nhất 8 ký tự.");
            
        if (!password.Any(char.IsDigit))
            throw new ArgumentException("Mật khẩu phải chứa ít nhất 1 chữ số.");
            
        if (!password.Any(char.IsUpper))
            throw new ArgumentException("Mật khẩu phải chứa ít nhất 1 chữ hoa.");

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            throw new ArgumentException("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.");
    }
}
