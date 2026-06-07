using MangaERP.Identity.Domain.Entities;

namespace MangaERP.Identity.Application.Ports;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    RefreshToken GenerateRefreshToken(Guid userId);
    Guid? ValidateRefreshToken(string token);
}
