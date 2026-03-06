using Microsoft.Extensions.Options;
using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.RateLimiting;

/// <summary>
/// Rate limiter using atomic increment per key (per IP + endpoint) with fixed-window TTL.
/// Uses IRateLimitBackend (Redis in production, in-memory in tests).
/// </summary>
public sealed class RedisRateLimitService : IRateLimitService
{
    private const string KeyPrefix = "ratelimit:";
    private readonly IRateLimitBackend _backend;
    private readonly RedisRateLimitOptions _options;

    public RedisRateLimitService(IRateLimitBackend backend, IOptions<RedisRateLimitOptions> options)
    {
        _backend = backend;
        _options = options.Value;
    }

    /// <summary>
    /// Returns true if the request is allowed, false if rate limit exceeded.
    /// </summary>
    public async Task<bool> TryAcquireAsync(string endpointKey, string clientId, CancellationToken cancellationToken = default)
    {
        var (maxAttempts, windowSeconds) = endpointKey switch
        {
            "create" => (_options.CreateSecretPerWindow, _options.WindowSeconds),
            "reveal" => (_options.RevealPerWindow, _options.WindowSeconds),
            _ => (100, 60)
        };

        var key = $"{KeyPrefix}{endpointKey}:{clientId}";
        var windowTtl = TimeSpan.FromSeconds(windowSeconds);
        var count = await _backend.IncrementAndGetAsync(key, windowTtl, cancellationToken).ConfigureAwait(false);
        return count <= maxAttempts;
    }
}

public sealed class RedisRateLimitOptions
{
    public const string SectionName = "RateLimiting";
    /// <summary>Max create requests per window per client (default 30).</summary>
    public int CreateSecretPerWindow { get; set; } = 30;
    /// <summary>Max reveal requests per window per client (default 15).</summary>
    public int RevealPerWindow { get; set; } = 15;
    /// <summary>Window duration in seconds (default 60).</summary>
    public int WindowSeconds { get; set; } = 60;
    /// <summary>When true, client IP is taken from X-Forwarded-For (only set true behind a trusted proxy that overwrites it). Default false.</summary>
    public bool TrustProxy { get; set; } = false;
}
