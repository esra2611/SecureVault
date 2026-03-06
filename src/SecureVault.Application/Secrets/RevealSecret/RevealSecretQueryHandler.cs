using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Domain.ValueObjects;

namespace SecureVault.Application.Secrets.RevealSecret;

public sealed class RevealSecretQueryHandler : IRequestHandler<RevealSecretQuery, RevealSecretHandlerOutcome>
{
    private const int DerivedKeyLength = 32;

    private readonly ISecretRepository _repository;
    private readonly IEncryptionService _encryption;
    private readonly ISecretCache _cache;
    private readonly ITimeProvider _time;
    private readonly IAuditPublisher _audit;
    private readonly IRevealSecurityConfig _securityConfig;
    private readonly IPasswordDerivation _passwordDerivation;
    private readonly ILogger<RevealSecretQueryHandler> _logger;

    public RevealSecretQueryHandler(
        ISecretRepository repository,
        IEncryptionService encryption,
        ISecretCache cache,
        ITimeProvider time,
        IAuditPublisher audit,
        IRevealSecurityConfig securityConfig,
        IPasswordDerivation passwordDerivation,
        ILogger<RevealSecretQueryHandler> logger)
    {
        _repository = repository;
        _encryption = encryption;
        _cache = cache;
        _time = time;
        _audit = audit;
        _securityConfig = securityConfig;
        _passwordDerivation = passwordDerivation;
        _logger = logger;
    }

    public async Task<RevealSecretHandlerOutcome> Handle(RevealSecretQuery request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHashFromRequest(request.Token);
        if (tokenHash is null)
            return new RevealSecretNotFoundOutcome();

        var utcNow = _time.UtcNow;
        if (await _cache.IsKnownExpiredAsync(tokenHash, cancellationToken))
            return new RevealSecretExpiredOutcome();

        var peekOutcome = await _repository.TryPeekSecretAsync(tokenHash, utcNow, cancellationToken);
        if (peekOutcome is TryPeekNotFoundOutcome)
            return new RevealSecretNotFoundOutcome();
        if (peekOutcome is TryPeekExpiredOrViewedOutcome)
            return new RevealSecretExpiredOutcome();
        if (peekOutcome is not TryPeekSuccessOutcome peekSuccess)
            return new RevealSecretNotFoundOutcome();

        var reveal = peekSuccess.Result;

        if (reveal.IsPasswordProtected)
        {
            if (string.IsNullOrEmpty(request.Password))
            {
                await Task.Delay(_securityConfig.DecryptionFailureDelay, cancellationToken);
                return new RevealSecretNotFoundOutcome();
            }
            if (reveal.SaltForPassword is null || reveal.PasswordHashBase64 is null)
            {
                await Task.Delay(_securityConfig.DecryptionFailureDelay, cancellationToken);
                return new RevealSecretNotFoundOutcome();
            }

            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(request.Password!.Trim());
            var computedVerificationHash = _passwordDerivation.DeriveKey(passwordBytes, reveal.SaltForPassword, DerivedKeyLength);
            bool hashMatches = false;
            try
            {
                var storedHashBytes = Convert.FromBase64String(reveal.PasswordHashBase64);
                hashMatches = storedHashBytes.Length == computedVerificationHash.Length && CryptographicOperations.FixedTimeEquals(storedHashBytes, computedVerificationHash);
            }
            catch
            {
                // Invalid stored hash encoding; treat as wrong password (constant-time path below).
            }

            if (!hashMatches)
            {
                _logger.LogWarning("Password verification failed for a secret (id not logged to avoid leakage).");
                await Task.Delay(_securityConfig.DecryptionFailureDelay, cancellationToken);
                return new RevealSecretNotFoundOutcome();
            }

            var consumed = await _repository.ConsumeAsync(reveal.SecretId, utcNow, cancellationToken);
            if (!consumed)
                return new RevealSecretExpiredOutcome();
        }
        else
        {
            // Non-password path: consume atomically with row lock so exactly one concurrent request succeeds.
            var onceOutcome = await _repository.TryRevealOnceAsync(tokenHash, utcNow, cancellationToken);
            if (onceOutcome is not TryRevealSuccessOutcome onceSuccess)
                return onceOutcome is TryRevealExpiredOrViewedOutcome ? new RevealSecretExpiredOutcome() : new RevealSecretNotFoundOutcome();
            reveal = onceSuccess.Result;
        }

        try
        {
            var plaintext = _encryption.Decrypt(reveal.Ciphertext, reveal.Nonce, reveal.KeyVersion);
            var tokenIdHint = Convert.ToBase64String(tokenHash.Value)[..Math.Min(12, Convert.ToBase64String(tokenHash.Value).Length)].Replace("+", "-").Replace("/", "_");
            await _audit.PublishRevealedAsync(reveal.SecretId, tokenIdHint, cancellationToken);

            _logger.LogInformation("Secret revealed. No secret or token logged.");
            return new RevealSecretSuccessOutcome(new RevealSecretResult(System.Text.Encoding.UTF8.GetString(plaintext)));
        }
        catch
        {
            _logger.LogWarning("Decryption failed for a secret (id not logged to avoid leakage).");
            await Task.Delay(_securityConfig.DecryptionFailureDelay, cancellationToken);
            return new RevealSecretNotFoundOutcome();
        }
    }

    /// <summary>
    /// Decode base64url token (32 bytes) and compute SHA-256 for DB lookup (we store hash in DB).
    /// </summary>
    internal static TokenHash? TokenHashFromRequest(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var decoded = token.Replace("-", "+").Replace("_", "/");
            var padding = decoded.Length % 4;
            if (padding != 0) decoded += new string('=', 4 - padding);
            var tokenBytes = Convert.FromBase64String(decoded);
            if (tokenBytes.Length != 32) return null;
            var hashBytes = SHA256.HashData(tokenBytes);
            return new TokenHash(hashBytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Token string to stored TokenHashBase64; for integration tests that need to query by hash. Same logic as DB lookup.</summary>
    internal static string? TokenHashBase64FromToken(string token) => TokenHashFromRequest(token)?.ToBase64();
}
