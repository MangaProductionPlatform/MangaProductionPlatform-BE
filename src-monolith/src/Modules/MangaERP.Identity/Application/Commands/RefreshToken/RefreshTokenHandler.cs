using MediatR;
using MangaERP.Identity.Application.Ports;

namespace MangaERP.Identity.Application.Commands.RefreshToken;

public record RefreshTokenCommand(string Token) : IRequest<RefreshTokenResult>;

/// <summary>
/// Result returned by RefreshTokenHandler.
/// AccessToken — put in response body.
/// NewRefreshToken — controller MUST write this to httpOnly cookie; never return in body.
/// </summary>
public record RefreshTokenResult(string AccessToken)
{
    public string NewRefreshToken { get; init; } = string.Empty;
}

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly IUserRepository _userRepo;
    private readonly ITokenService _tokenService;

    public RefreshTokenHandler(
        IRefreshTokenRepository refreshRepo,
        IUserRepository userRepo,
        ITokenService tokenService)
    {
        _refreshRepo  = refreshRepo;
        _userRepo     = userRepo;
        _tokenService = tokenService;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // 1. Look up token in DB (GetByTokenAsync already filters IsRevoked == false)
        var existingToken = await _refreshRepo.GetByTokenAsync(request.Token, cancellationToken)
            ?? throw new UnauthorizedAccessException("Refresh token is invalid or has been revoked.");

        // 2. Validate expiry
        if (existingToken.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired. Please log in again.");

        // 3. Load the associated user
        var user = await _userRepo.GetByIdAsync(existingToken.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User account no longer exists.");

        // 4. Revoke the current token (one-time use / token rotation)
        existingToken.IsRevoked = true;
        existingToken.RevokedAt = DateTime.UtcNow;
        await _refreshRepo.UpdateAsync(existingToken, cancellationToken);

        // 5. Issue new token pair
        var newAccessToken  = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken(user.Id);
        await _refreshRepo.AddAsync(newRefreshToken, cancellationToken);

        return new RefreshTokenResult(newAccessToken)
        {
            NewRefreshToken = newRefreshToken.Token
        };
    }
}
