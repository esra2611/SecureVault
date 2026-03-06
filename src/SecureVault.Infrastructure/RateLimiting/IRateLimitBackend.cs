namespace SecureVault.Infrastructure.RateLimiting;

/// <summary>
/// Abstraction for atomic increment-and-get with TTL (used by rate limiter).
/// Enables Redis in production and in-memory fake in unit tests.
/// </summary>
public interface IRateLimitBackend
{
    /// <summary>
    /// Atomically increments the counter for the key and returns the new value.
    /// If the key did not exist, it is created with the given TTL.
    /// </summary>
    Task<long> IncrementAndGetAsync(string key, TimeSpan windowTtl, CancellationToken cancellationToken = default);
}
