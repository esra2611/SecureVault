using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Application.Common.Models;
using SecureVault.Domain.ValueObjects;

namespace SecureVault.Application.Secrets.CreateSecret;

public sealed class CreateSecretCommandHandler : IRequestHandler<CreateSecretCommand, CreateSecretResult>
{
    private const int SaltSize = 16;
    private const int DerivedKeyLength = 32;

    private readonly ISecretRepository _repository;
    private readonly IEncryptionService _encryption;
    private readonly IKeyProvider _keyProvider;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ISecretCache _cache;
    private readonly IAuditPublisher _audit;
    private readonly ITimeProvider _time;
    private readonly ICreateSecretLinkBuilder _linkBuilder;
    private readonly ISecretExpiryConfig _expiryConfig;
    private readonly IPasswordDerivation _passwordDerivation;
    private readonly ILogger<CreateSecretCommandHandler> _logger;

    public CreateSecretCommandHandler(
        ISecretRepository repository,
        IEncryptionService encryption,
        IKeyProvider keyProvider,
        ITokenGenerator tokenGenerator,
        ISecretCache cache,
        IAuditPublisher audit,
        ITimeProvider time,
        ICreateSecretLinkBuilder linkBuilder,
        ISecretExpiryConfig expiryConfig,
        IPasswordDerivation passwordDerivation,
        ILogger<CreateSecretCommandHandler> logger)
    {
        _repository = repository;
        _encryption = encryption;
        _keyProvider = keyProvider;
        _tokenGenerator = tokenGenerator;
        _cache = cache;
        _audit = audit;
        _time = time;
        _linkBuilder = linkBuilder;
        _expiryConfig = expiryConfig;
        _passwordDerivation = passwordDerivation;
        _logger = logger;
    }

    public async Task<CreateSecretResult> Handle(CreateSecretCommand request, CancellationToken cancellationToken)
    {
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(request.Plaintext);
        byte[]? salt = null;
        var isPasswordProtected = false;
        string? passwordHashBase64 = null;

        if (!string.IsNullOrEmpty(request.Password))
        {
            salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);
            var passwordBytes = System.Text.Encoding.UTF8.GetBytes(request.Password!.Trim());
            var verificationHash = _passwordDerivation.DeriveKey(passwordBytes, salt, DerivedKeyLength);
            passwordHashBase64 = Convert.ToBase64String(verificationHash);
            isPasswordProtected = true;
        }

        var (ciphertext, nonce) = _encryption.Encrypt(plaintextBytes);

        var (tokenBytes, tokenHash) = _tokenGenerator.Generate();
        var utcNow = _time.UtcNow;
        var expiresAt = ComputeExpiresAt(request.ExpiryType, utcNow, _expiryConfig.OverrideTtlForTests);

        var keyVersion = _keyProvider.GetCurrentVersion();
        var id = await _repository.AddAsync(
            new AddSecretRequest
            {
                TokenHash = tokenHash,
                ExpiryType = request.ExpiryType,
                UtcExpiresAt = expiresAt,
                Ciphertext = ciphertext,
                Nonce = nonce,
                KeyVersion = keyVersion,
                SaltForPassword = salt,
                IsPasswordProtected = isPasswordProtected,
                PasswordHashBase64 = passwordHashBase64
            },
            cancellationToken);

        var ttl = expiresAt - utcNow;
        if (ttl > TimeSpan.Zero)
        {
            try
            {
                await _cache.SetSecretTtlAsync(tokenHash, ttl, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache SetSecretTtlAsync failed (best-effort); secret created successfully. DB is source of truth for expiry.");
            }
        }

        var tokenIdHint = Convert.ToBase64String(tokenHash.Value)[..Math.Min(12, Convert.ToBase64String(tokenHash.Value).Length)].Replace("+", "-").Replace("/", "_");
        await _audit.PublishCreatedAsync(id, tokenIdHint, expiresAt, cancellationToken);

        var shareUrl = _linkBuilder.Build(tokenBytes);
        _logger.LogInformation("Secret created. Id: {SecretId}, ExpiresAt: {ExpiresAt}. No secret or token logged.", id, expiresAt);

        return new CreateSecretResult(shareUrl, tokenIdHint);
    }

    private static DateTime ComputeExpiresAt(ExpiryType expiryType, DateTime utcNow, TimeSpan? overrideTtlForTests)
    {
        if (expiryType == ExpiryType.BurnAfterRead)
            return utcNow.AddYears(10); // Long window; "expiry" is first read
        if (overrideTtlForTests.HasValue)
            return utcNow.Add(overrideTtlForTests.Value);
        return expiryType switch
        {
            ExpiryType.OneHour => utcNow.AddHours(1),
            ExpiryType.TwentyFourHours => utcNow.AddHours(24),
            ExpiryType.SevenDays => utcNow.AddDays(7),
            _ => utcNow.AddHours(24)
        };
    }
}
