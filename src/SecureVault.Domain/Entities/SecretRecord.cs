using SecureVault.Domain.ValueObjects;

namespace SecureVault.Domain.Entities;

/// <summary>
/// Domain representation of a stored secret (metadata only; ciphertext lives in persistence).
/// Used for audit and domain logic; never carries plaintext or ciphertext in domain events.
/// Password hash/salt are used only for verification; raw password is never exposed outside the aggregate.
/// </summary>
public sealed class SecretRecord
{
    public Guid Id { get; }
    public TokenHash TokenHash { get; }
    public ExpiryType ExpiryType { get; }
    public DateTime UtcCreatedAt { get; }
    public DateTime UtcExpiresAt { get; }
    public DateTime? UtcRevealedAt { get; }
    /// <summary>True when secret requires a password to reveal; hash and salt must be present.</summary>
    public bool IsPasswordProtected { get; }
    /// <summary>Base64 of the stored password-derived hash; only set when <see cref="IsPasswordProtected"/> is true.</summary>
    public string? PasswordHash { get; }
    /// <summary>Base64 of the salt used for password derivation; only set when <see cref="IsPasswordProtected"/> is true.</summary>
    public string? PasswordSalt { get; }

    public SecretRecord(
        Guid id,
        TokenHash tokenHash,
        ExpiryType expiryType,
        DateTime utcCreatedAt,
        DateTime utcExpiresAt,
        DateTime? utcRevealedAt,
        bool isPasswordProtected = false,
        string? passwordHash = null,
        string? passwordSalt = null)
    {
        if (isPasswordProtected && (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(passwordSalt)))
            throw new ArgumentException("When IsPasswordProtected is true, PasswordHash and PasswordSalt must be set.", nameof(isPasswordProtected));

        Id = id;
        TokenHash = tokenHash;
        ExpiryType = expiryType;
        UtcCreatedAt = utcCreatedAt;
        UtcExpiresAt = utcExpiresAt;
        UtcRevealedAt = utcRevealedAt;
        IsPasswordProtected = isPasswordProtected;
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
    }

    public bool IsExpired(DateTime utcNow) => utcNow >= UtcExpiresAt;
    public bool IsRevealed => UtcRevealedAt.HasValue;
    public bool CanReveal(DateTime utcNow) => !IsRevealed && !IsExpired(utcNow);
}
