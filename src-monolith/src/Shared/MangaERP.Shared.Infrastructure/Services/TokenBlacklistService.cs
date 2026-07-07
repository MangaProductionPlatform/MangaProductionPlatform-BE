using System;
using MangaERP.Shared.Application.Ports;
using Microsoft.Extensions.Caching.Memory;

namespace MangaERP.Shared.Infrastructure.Services;

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _cache;
    private const string KeyPrefix = "blacklist:token:";

    public TokenBlacklistService(IMemoryCache cache) => _cache = cache;

    public void Blacklist(string jti, DateTime accessTokenExpiryUtc)
    {
        var ttl = accessTokenExpiryUtc - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero) return; // Already naturally expired, no need to blacklist

        // Set cache with TTL equal to the remaining lifetime of access token.
        // It will automatically clean up when TTL expires.
        _cache.Set($"{KeyPrefix}{jti}", true, ttl);
    }

    public bool IsBlacklisted(string jti) =>
        _cache.TryGetValue($"{KeyPrefix}{jti}", out _);
}
