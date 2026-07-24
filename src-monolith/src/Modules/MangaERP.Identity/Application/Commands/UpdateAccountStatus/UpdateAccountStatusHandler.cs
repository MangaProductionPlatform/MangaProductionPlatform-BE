using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Identity.Application.Commands.UpdateAccountStatus;

public record UpdateAccountStatusCommand(
    Guid UserId,
    AccountStatus Status
) : IRequest<UpdateAccountStatusResult>;

public record UpdateAccountStatusResult(
    Guid UserId,
    string Username,
    string Status
);

public class UpdateAccountStatusHandler : IRequestHandler<UpdateAccountStatusCommand, UpdateAccountStatusResult>
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;

    public UpdateAccountStatusHandler(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshTokenRepo)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
    }

    public async Task<UpdateAccountStatusResult> Handle(UpdateAccountStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        if (user.AccountStatus != request.Status)
        {
            if (request.Status == AccountStatus.Suspended || request.Status == AccountStatus.Deactivated)
            {
                var isTantou = user.Role == UserRole.TantouEditor || await _userRepo.HasRbacRoleAsync(user.Id, RoleNames.TantouEditor, cancellationToken);
                if (isTantou && await _userRepo.HasAssignedMangakasAsync(user.Id, cancellationToken))
                {
                    throw new InvalidOperationException("Cannot suspend or deactivate a Tantou Editor currently managing one or more active Mangakas. Reassign their Mangakas first.");
                }

                await _refreshTokenRepo.RevokeAllForUserAsync(user.Id, cancellationToken);
            }

            user.AccountStatus = request.Status;
            await _userRepo.UpdateAsync(user, cancellationToken);
        }

        return new UpdateAccountStatusResult(user.Id, user.Username, user.AccountStatus.ToString());
    }
}
