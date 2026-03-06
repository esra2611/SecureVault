using SecureVault.Application.Common.Models;
using SecureVault.Domain.Entities;
using SecureVault.Domain.ValueObjects;

namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Persistence for secrets. Ciphertext and nonce stored at rest; token as hash only.
/// </summary>
public interface ISecretRepository
{
    Task<Guid> AddAsync(AddSecretRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads secret by token for reveal without consuming it. Use with ConsumeAsync after password verification so wrong password does not burn the secret.
    /// </summary>
    Task<TryPeekSecretOutcome> TryPeekSecretAsync(TokenHash tokenHash, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the secret as revealed and clears ciphertext/nonce. Returns true if this call consumed the secret; false if already consumed (e.g. by another request).
    /// </summary>
    Task<bool> ConsumeAsync(Guid secretId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically: find by token hash, ensure not revealed and not expired, mark revealed and clear ciphertext, return ciphertext and nonce.
    /// Returns Success(revealResult) when exactly one reveal is performed; ExpiredOrAlreadyViewed when secret exists but is no longer available; NotFound when token is invalid.
    /// </summary>
    Task<TryRevealOnceOutcome> TryRevealOnceAsync(TokenHash tokenHash, DateTime utcNow, CancellationToken cancellationToken = default);

    Task<bool> ExistsAndNotExpiredAsync(TokenHash tokenHash, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Deletes rows that are no longer revealable (expired or already revealed). Safe and idempotent; used by cleanup job.</summary>
    Task<int> DeleteTerminalRowsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

public sealed record RevealResult(
    Guid SecretId,
    byte[] Ciphertext,
    byte[] Nonce,
    int KeyVersion,
    byte[]? SaltForPassword,
    bool IsPasswordProtected,
    string? PasswordHashBase64);

/// <summary>Result of peek (load without consume): success with reveal data, expired/already viewed, or not found.</summary>
public abstract record TryPeekSecretOutcome;

public sealed record TryPeekSuccessOutcome(RevealResult Result) : TryPeekSecretOutcome;

public sealed record TryPeekExpiredOrViewedOutcome : TryPeekSecretOutcome;

public sealed record TryPeekNotFoundOutcome : TryPeekSecretOutcome;

/// <summary>Result of atomic reveal attempt: success, expired/already viewed (410), or not found (404).</summary>
public abstract record TryRevealOnceOutcome;

public sealed record TryRevealSuccessOutcome(RevealResult Result) : TryRevealOnceOutcome;

public sealed record TryRevealExpiredOrViewedOutcome : TryRevealOnceOutcome;

public sealed record TryRevealNotFoundOutcome : TryRevealOnceOutcome;
