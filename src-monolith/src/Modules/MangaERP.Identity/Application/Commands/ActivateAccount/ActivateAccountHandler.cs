using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Identity.Application.Commands.ActivateAccount;

public record ActivateAccountCommand(
    string Token,
    string Password,
    string? PenName = null,
    string[]? DrawingSoftwares = null,
    string? BankAccountNumber = null
) : IRequest<ActivateAccountResult>;

public record ActivateAccountResult(Guid UserId, string Username, string Role);

public class ActivateAccountHandler : IRequestHandler<ActivateAccountCommand, ActivateAccountResult>
{
    private readonly IUserRepository _userRepo;
    private readonly IInvitationTokenRepository _invTokenRepo;
    private readonly ITokenService _tokenService;

    public ActivateAccountHandler(
        IUserRepository userRepo,
        IInvitationTokenRepository invTokenRepo,
        ITokenService tokenService)
    {
        _userRepo = userRepo;
        _invTokenRepo = invTokenRepo;
        _tokenService = tokenService;
    }

    public async Task<ActivateAccountResult> Handle(ActivateAccountCommand request, CancellationToken cancellationToken)
    {
        // Step 1: Validate JWT invitation token claims
        var (isValid, userId, _, username, role) = _tokenService.ValidateInvitationToken(request.Token);
        if (!isValid)
            throw new InvalidInvitationTokenException();

        // Step 2: Check DB record (not used, not expired)
        var invToken = await _invTokenRepo.GetByTokenStringAsync(request.Token, cancellationToken)
            ?? throw new InvalidInvitationTokenException();

        if (invToken.IsUsed || invToken.ExpiresAt < DateTime.UtcNow)
            throw new InvalidInvitationTokenException();

        // Step 3: Load user
        var user = await _userRepo.GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        // Step 4: Check account is still pending
        if (user.AccountStatus != AccountStatus.PendingActivation)
            throw new AccountAlreadyActivatedException();

        // Step 5: Set password and activate
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        user.AccountStatus = AccountStatus.Active;

        if (!string.IsNullOrWhiteSpace(request.PenName))
        {
            user.PenName = request.PenName.Trim();
        }

        if (request.DrawingSoftwares != null && request.DrawingSoftwares.Length > 0)
        {
            user.DrawingSoftwares = string.Join(",", request.DrawingSoftwares);
        }

        if (!string.IsNullOrWhiteSpace(request.BankAccountNumber))
        {
            user.BankAccountNumber = request.BankAccountNumber.Trim();
        }

        await _userRepo.UpdateAsync(user, cancellationToken);

        // Step 6: Mark invitation token as used
        invToken.IsUsed = true;
        invToken.UsedAt = DateTime.UtcNow;
        await _invTokenRepo.UpdateAsync(invToken, cancellationToken);

        return new ActivateAccountResult(user.Id, user.Username, user.Role.ToString());
    }
}
