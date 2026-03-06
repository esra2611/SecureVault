namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Rate limiting for create/reveal endpoints. Abstraction so API and tests do not depend on Redis.
/// On backend failure, implementors may fail open (allow) or fail closed (deny); document behavior.
/// </summary>
public interface IRateLimitService
{
    /// <summary>Returns true if the request is allowed, false if rate limit exceeded.</summary>
    Task<bool> TryAcquireAsync(string endpointKey, string clientId, CancellationToken cancellationToken = default);
}
