using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Tests.Integration.TestDoubles;

/// <summary>No-op rate limiter for integration tests so all requests are allowed (avoids 429 from shared client identity).</summary>
public sealed class AllowAllRateLimitService : IRateLimitService
{
    public Task<bool> TryAcquireAsync(string endpointKey, string clientId, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<bool>(cancellationToken);
        return Task.FromResult(true);
    }
}
