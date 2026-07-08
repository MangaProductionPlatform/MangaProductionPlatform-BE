using MangaERP.Identity.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MediatR;

namespace MangaERP.Identity.Application.Commands.ChangePassword;

// ── Command ───────────────────────────────────────────────────────────────────
public record ChangePasswordCommand(
    Guid   UserId,
    string CurrentPassword,
    string NewPassword
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────
public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IUserRepository _userRepo;

    public ChangePasswordHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Mật khẩu mới phải có ít nhất 8 ký tự.");

        var user = await _userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new EntityNotFoundException("User", request.UserId);

        // Verify current password using BCrypt (same library as ActivateAccountHandler)
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepo.UpdateAsync(user, ct);
    }
}
