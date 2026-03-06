using System.Security.Cryptography;

namespace SecureVault.Domain.ValueObjects;

/// <summary>
/// SHA-256 hash of the shareable token. Stored in DB; never store plaintext token.
/// </summary>
public sealed class TokenHash : IEquatable<TokenHash>
{
    public byte[] Value { get; }

    public TokenHash(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 32)
            throw new ArgumentException("Token hash must be 32 bytes (SHA-256).", nameof(value));
        Value = value;
    }

    public static TokenHash FromBytes(byte[] value) => new(value);
    public static TokenHash FromBase64(string base64) => new(Convert.FromBase64String(base64));

    public string ToBase64() => Convert.ToBase64String(Value);

    /// <summary>Constant-time comparison to prevent timing side-channels.</summary>
    public bool Equals(TokenHash? other) => other is not null && Value.Length == other.Value.Length && CryptographicOperations.FixedTimeEquals(Value.AsSpan(), other.Value.AsSpan());
    public override bool Equals(object? obj) => obj is TokenHash other && Equals(other);
    public override int GetHashCode() => Value.Length > 0 ? HashCode.Combine(Value[0], Value[1], Value[^1]) : 0;
}
