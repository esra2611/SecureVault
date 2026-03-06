using SecureVault.Application.Common.Interfaces;
using SecureVault.Domain.ValueObjects;

namespace SecureVault.Tests.Integration;

/// <summary>No-op cache for integration tests that assert DB is source of truth (cache does not change correctness).</summary>
public sealed class NoOpSecretCache : ISecretCache
{
    public Task SetSecretTtlAsync(TokenHash tokenHash, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> IsKnownExpiredAsync(TokenHash tokenHash, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
