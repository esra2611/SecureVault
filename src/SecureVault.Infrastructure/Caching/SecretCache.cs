using Microsoft.Extensions.Caching.Distributed;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Domain.ValueObjects;

namespace SecureVault.Infrastructure.Caching;

public sealed class SecretCache : ISecretCache
{
    private const string Prefix = "secret:ttl:";
    private readonly IDistributedCache _cache;

    public SecretCache(IDistributedCache cache) => _cache = cache;

    public async Task SetSecretTtlAsync(TokenHash tokenHash, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var key = Prefix + tokenHash.ToBase64();
        await _cache.SetAsync(key, [1], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken);
    }

    /// <summary>Returns true only if we have a positive "expired" signal (e.g. key was deleted after TTL). We never treat "missing" as proof of expiry; DB is source of truth.</summary>
    public async Task<bool> IsKnownExpiredAsync(TokenHash tokenHash, CancellationToken cancellationToken = default)
    {
        var key = Prefix + tokenHash.ToBase64();
        var value = await _cache.GetAsync(key, cancellationToken);
        return false; // We do not short-circuit reveal on cache; DB is authoritative. Cache is for TTL write path only.
    }
}
