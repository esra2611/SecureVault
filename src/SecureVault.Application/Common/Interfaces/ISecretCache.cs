using SecureVault.Domain.ValueObjects;

namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Redis-backed cache for fast "already expired" check and rate limiting keys.
/// TTL is advisory; DB is source of truth for expiry.
/// </summary>
public interface ISecretCache
{
    Task SetSecretTtlAsync(TokenHash tokenHash, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> IsKnownExpiredAsync(TokenHash tokenHash, CancellationToken cancellationToken = default);
}
