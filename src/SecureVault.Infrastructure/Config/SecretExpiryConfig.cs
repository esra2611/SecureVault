using Microsoft.Extensions.Options;
using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.Config;

public sealed class SecretExpiryConfig : ISecretExpiryConfig
{
    public const string SectionName = "SecureVault";

    public SecretExpiryConfig(IOptions<SecretExpiryOptions> options)
    {
        var seconds = options.Value.OverrideTtlSeconds;
        OverrideTtlForTests = seconds is > 0 and < 3600 * 24
            ? TimeSpan.FromSeconds(seconds.Value)
            : null;
    }

    public TimeSpan? OverrideTtlForTests { get; }
}

public sealed class SecretExpiryOptions
{
    public const string SectionName = "SecureVault";

    /// <summary>Optional. When set (e.g. in test), non-burn expiry uses this many seconds. Must be 1–86400. Production should not set.</summary>
    public int? OverrideTtlSeconds { get; set; }
}
