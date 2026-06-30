using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;

namespace MangaERP.Identity.Application.Commands.Logout;

/// <summary>
/// Revokes a specific refresh token (identified by its string value from the cookie).
/// This approach works even when the access token has already expired,
/// because we do not require a valid JWT claim — only the cookie token.
/// </summary>
public record LogoutCommand(string RefreshToken) : IRequest<LogoutResult>;

public record LogoutResult(string Message);

public class LogoutHandler : IRequestHandler<LogoutCommand, LogoutResult>
{
    private readonly IRefreshTokenRepository _refreshRepo;

    public LogoutHandler(IRefreshTokenRepository refreshRepo)
    {
        _refreshRepo = refreshRepo;
    }

    public async Task<LogoutResult> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Look up the specific token from the cookie
        var token = await _refreshRepo.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (token is not null)
        {
            // Revoke this specific token (not all tokens for the user)
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            await _refreshRepo.UpdateAsync(token, cancellationToken);
        }

        // Even if token is not found (already revoked or expired), logout succeeds.
        // The controller will still delete the cookie.
        return new LogoutResult("Logged out successfully.");
    }
}
