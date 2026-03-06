namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Derives a verification value from a password using PBKDF2.
/// Used for optional password protection as an access gate only: the derived value is stored (e.g. Base64) and compared on reveal.
/// The derived value must NEVER be used as an encryption or decryption key; secret payload is always encrypted with the server master key.
/// Implementations must use cryptographically secure salt and sufficient iterations (e.g. >= 100,000).
/// </summary>
public interface IPasswordDerivation
{
    /// <summary>
    /// Derives a fixed-length verification value from password and salt using PBKDF2 (SHA256, high iteration count).
    /// Caller must not store the raw password; store Base64(returned value) for constant-time verification on reveal.
    /// The returned value must not be passed to encryption or decryption; it is for verification only.
    /// </summary>
    /// <param name="passwordUtf8">Password as UTF-8 bytes; must not be null or empty.</param>
    /// <param name="salt">Cryptographically random salt (e.g. >= 16 bytes).</param>
    /// <param name="outputLength">Output length in bytes (e.g. 32 for consistency with verification storage).</param>
    /// <returns>Derived value for verification only; same input always produces same output for constant-time comparison.</returns>
    byte[] DeriveKey(byte[] passwordUtf8, byte[] salt, int outputLength);
}
