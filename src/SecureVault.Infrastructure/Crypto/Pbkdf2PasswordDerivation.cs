using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.Crypto;

/// <summary>
/// PBKDF2 (RFC 2898) with SHA-256 for password-derived key and verification hash.
/// OWASP-recommended minimum 100,000 iterations for password hashing; salt >= 16 bytes.
/// </summary>
public sealed class Pbkdf2PasswordDerivation : IPasswordDerivation
{
    private readonly int _iterations;

    public Pbkdf2PasswordDerivation(IOptions<Pbkdf2Options> options)
    {
        _iterations = Math.Max(Pbkdf2Options.MinIterations, options.Value.Iterations);
    }

    public byte[] DeriveKey(byte[] passwordUtf8, byte[] salt, int outputLength)
    {
        if (passwordUtf8 is null || passwordUtf8.Length == 0)
            throw new ArgumentException("Password must not be null or empty.", nameof(passwordUtf8));
        if (salt is null || salt.Length < 16)
            throw new ArgumentException("Salt must be at least 16 bytes.", nameof(salt));
        if (outputLength < 1 || outputLength > 128)
            throw new ArgumentOutOfRangeException(nameof(outputLength), "Output length must be between 1 and 128.");

        return Rfc2898DeriveBytes.Pbkdf2(passwordUtf8, salt, _iterations, HashAlgorithmName.SHA256, outputLength);
    }
}

public sealed class Pbkdf2Options
{
    public const string SectionName = "Security:Pbkdf2";
    /// <summary>OWASP recommends >= 100,000 for PBKDF2-SHA256.</summary>
    public const int MinIterations = 100_000;

    public int Iterations { get; set; } = 100_000;
}
