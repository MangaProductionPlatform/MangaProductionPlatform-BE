using MangaERP.Identity.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MediatR;

namespace MangaERP.Identity.Application.Commands.ChangeAvatar;

// ── Command ───────────────────────────────────────────────────────────────────
public record ChangeAvatarCommand(Guid UserId, string AvatarUrl) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────
public class ChangeAvatarHandler : IRequestHandler<ChangeAvatarCommand>
{
    private readonly IUserRepository _userRepo;

    public ChangeAvatarHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task Handle(ChangeAvatarCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AvatarUrl))
            throw new ArgumentException("URL ảnh đại diện không được để trống.");

        if (!Uri.TryCreate(request.AvatarUrl, UriKind.Absolute, out _))
            throw new ArgumentException("URL ảnh đại diện không hợp lệ.");

        var user = await _userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new EntityNotFoundException("User", request.UserId);

        user.AvatarUrl = request.AvatarUrl;
        await _userRepo.UpdateAsync(user, ct);
    }
}
