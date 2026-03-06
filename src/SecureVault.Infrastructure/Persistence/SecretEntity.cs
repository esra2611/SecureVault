namespace SecureVault.Infrastructure.Persistence;

public sealed class SecretEntity
{
    public Guid Id { get; set; }
    /// <summary>Base64 of SHA-256(token). Stored as string for indexing and comparison.</summary>
    public string TokenHashBase64 { get; set; } = string.Empty;
    public int ExpiryType { get; set; }
    public DateTime UtcCreatedAt { get; set; }
    public DateTime UtcExpiresAt { get; set; }
    public DateTime? UtcRevealedAt { get; set; }
    public byte[]? Ciphertext { get; set; }
    public byte[]? Nonce { get; set; }
    public byte[]? SaltForPassword { get; set; }
    /// <summary>Encryption key version used for this secret (for key rotation / backward decryption).</summary>
    public int KeyVersion { get; set; } = 1;
    /// <summary>True when secret requires password to reveal; PasswordHashBase64 and SaltForPassword must be set.</summary>
    public bool IsPasswordProtected { get; set; }
    /// <summary>Base64 of PBKDF2-derived hash (verification only); set when IsPasswordProtected is true.</summary>
    public string? PasswordHashBase64 { get; set; }
}
