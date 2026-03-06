using StackExchange.Redis;

namespace SecureVault.Infrastructure.RateLimiting;

/// <summary>
/// Redis implementation of rate limit counter using atomic INCR + EXPIRE.
/// </summary>
public sealed class RedisRateLimitBackend : IRateLimitBackend
{
    private readonly IConnectionMultiplexer _redis;

    public RedisRateLimitBackend(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<long> IncrementAndGetAsync(string key, TimeSpan windowTtl, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var count = await db.StringIncrementAsync(key).ConfigureAwait(false);
        if (count == 1)
            await db.KeyExpireAsync(key, windowTtl).ConfigureAwait(false);
        return count;
    }
}
