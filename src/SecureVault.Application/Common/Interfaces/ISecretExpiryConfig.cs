namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Optional configuration for secret expiry. Used only in test environments to override TTL (e.g. 2–5 seconds) for integration tests.
/// When null, production behaviour is unchanged.
/// </summary>
public interface ISecretExpiryConfig
{
    /// <summary>When set (e.g. in test), non–burn expiry modes use this TTL instead of 1h/24h/7d. Production should not set this.</summary>
    TimeSpan? OverrideTtlForTests { get; }
}
