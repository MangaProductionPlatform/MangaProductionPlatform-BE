using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Identity.Application.Commands.DeleteAccount;

public record DeleteAccountCommand(
    Guid UserId
) : IRequest;

public class DeleteAccountHandler : IRequestHandler<DeleteAccountCommand>
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;

    public DeleteAccountHandler(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshTokenRepo)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
    }

    public async Task Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        var isTantou = user.Role == UserRole.TantouEditor || await _userRepo.HasRbacRoleAsync(user.Id, RoleNames.TantouEditor, cancellationToken);
        if (isTantou && await _userRepo.HasAssignedMangakasAsync(user.Id, cancellationToken))
        {
            throw new InvalidOperationException("Cannot delete a Tantou Editor currently managing one or more active Mangakas. Reassign their Mangakas first.");
        }

        // Revoke all refresh tokens
        await _refreshTokenRepo.RevokeAllForUserAsync(user.Id, cancellationToken);

        // Soft delete the user
        await _userRepo.DeleteAsync(user, cancellationToken);
    }
}
