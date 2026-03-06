using SecureVault.Domain.ValueObjects;

namespace SecureVault.Application.Common.Models;

/// <summary>
/// Parameter object for ISecretRepository.AddAsync to satisfy S107 (methods should not have too many parameters).
/// </summary>
public sealed class AddSecretRequest
{
    public required TokenHash TokenHash { get; init; }
    public required ExpiryType ExpiryType { get; init; }
    public required DateTime UtcExpiresAt { get; init; }

    public required byte[] Ciphertext { get; init; }
    public required byte[] Nonce { get; init; }

    public required int KeyVersion { get; init; }

    public byte[]? SaltForPassword { get; init; }

    public bool IsPasswordProtected { get; init; }

    public string? PasswordHashBase64 { get; init; }
}
