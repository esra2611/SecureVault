namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Provides encryption keys by version for key rotation. Enables decryption of secrets
/// encrypted with older keys after rotation (NIST SP 800-57, OWASP Cryptographic Storage).
/// </summary>
public interface IKeyProvider
{
    /// <summary>Current key version used for new encryptions.</summary>
    int GetCurrentVersion();

    /// <summary>Returns 256-bit (32-byte) key for the given version. Throws if version unknown.</summary>
    byte[] GetKey(int version);
}
