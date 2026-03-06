using Microsoft.Extensions.Options;
using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.Config;

/// <summary>
/// Provides reveal security settings (e.g. constant-time delay on decryption failure to reduce timing oracle).
/// </summary>
public sealed class RevealSecurityConfig : IRevealSecurityConfig
{
    public RevealSecurityConfig(IOptions<RevealSecurityOptions> options)
    {
        var ms = options.Value.RevealDecryptionFailureDelayMs;
        DecryptionFailureDelay = ms is > 0 and <= 5000
            ? TimeSpan.FromMilliseconds(ms.Value)
            : TimeSpan.FromMilliseconds(100);
    }

    public TimeSpan DecryptionFailureDelay { get; }
}

public sealed class RevealSecurityOptions
{
    public const string SectionName = "Security:Reveal";

    /// <summary>Delay in ms after decryption failure before returning 404 (reduces timing oracle: wrong password vs token not found). Default 100ms.</summary>
    public int? RevealDecryptionFailureDelayMs { get; set; }
}
