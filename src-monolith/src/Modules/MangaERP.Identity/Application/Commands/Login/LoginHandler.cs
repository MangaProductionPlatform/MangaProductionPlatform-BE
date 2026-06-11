using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;

namespace MangaERP.Identity.Application.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public record LoginResult(string AccessToken, string RefreshToken, string Role, Guid UserId);

public class LoginHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly ITokenService _tokenService;

    public LoginHandler(IUserRepository userRepo, IRefreshTokenRepository refreshRepo, ITokenService tokenService)
    {
        _userRepo = userRepo;
        _refreshRepo = refreshRepo;
        _tokenService = tokenService;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepo.GetByEmailAsync(request.Email, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid credentials.");

        if (user.AccountStatus == AccountStatus.PendingActivation)
            throw new UnauthorizedAccessException("Account not yet activated. Check your invitation email.");

        if (user.AccountStatus != AccountStatus.Active)
            throw new UnauthorizedAccessException("Account is suspended or deactivated.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken(user.Id);
        await _refreshRepo.AddAsync(refreshToken, cancellationToken);

        return new LoginResult(accessToken, refreshToken.Token, user.Role.ToString(), user.Id);
    }
}
