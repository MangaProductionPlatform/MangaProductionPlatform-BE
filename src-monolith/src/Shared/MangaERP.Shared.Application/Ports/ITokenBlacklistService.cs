using System;

namespace MangaERP.Shared.Application.Ports;

public interface ITokenBlacklistService
{
    void Blacklist(string jti, DateTime accessTokenExpiryUtc);
    bool IsBlacklisted(string jti);
}
