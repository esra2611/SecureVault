namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Security-related config for reveal flow (e.g. constant-time delay on failure to reduce timing oracle).
/// </summary>
public interface IRevealSecurityConfig
{
    /// <summary>Delay to apply after decryption failure before returning, to minimize timing side-channel (wrong password vs not found).</summary>
    TimeSpan DecryptionFailureDelay { get; }
}
