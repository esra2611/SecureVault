using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Application.Secrets.RevealSecret;
using SecureVault.Domain.ValueObjects;
using Xunit;

namespace SecureVault.Tests.Unit;

public class RevealSecretQueryHandlerTests
{
    private readonly ISecretRepository _repository;
    private readonly IEncryptionService _encryption;
    private readonly ISecretCache _cache;
    private readonly ITimeProvider _time;
    private readonly IAuditPublisher _audit;
    private readonly IRevealSecurityConfig _securityConfig;
    private readonly IPasswordDerivation _passwordDerivation;
    private readonly ILogger<RevealSecretQueryHandler> _logger;
    private readonly RevealSecretQueryHandler _sut;

    private static readonly DateTime FixedUtc = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TokenHash SampleHash = TokenHash.FromBytes(new byte[32]);

    public RevealSecretQueryHandlerTests()
    {
        _repository = Substitute.For<ISecretRepository>();
        _encryption = Substitute.For<IEncryptionService>();
        _cache = Substitute.For<ISecretCache>();
        _time = Substitute.For<ITimeProvider>();
        _audit = Substitute.For<IAuditPublisher>();
        _securityConfig = Substitute.For<IRevealSecurityConfig>();
        _securityConfig.DecryptionFailureDelay.Returns(TimeSpan.Zero);
        _passwordDerivation = Substitute.For<IPasswordDerivation>();
        _logger = Substitute.For<ILogger<RevealSecretQueryHandler>>();
        _time.UtcNow.Returns(FixedUtc);
        _sut = new RevealSecretQueryHandler(_repository, _encryption, _cache, _time, _audit, _securityConfig, _passwordDerivation, _logger);
    }

    private static RevealResult MakeRevealResult(Guid id, byte[] ciphertext, byte[] nonce, int keyVersion, byte[]? salt = null, bool isPasswordProtected = false, string? passwordHashBase64 = null)
        => new(id, ciphertext, nonce, keyVersion, salt, isPasswordProtected, passwordHashBase64);

    [Fact]
    public async Task Handle_returns_NotFoundOutcome_when_token_is_null()
    {
        var query = new RevealSecretQuery(null!);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_NotFoundOutcome_when_token_is_empty()
    {
        var query = new RevealSecretQuery("");

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_NotFoundOutcome_when_token_is_whitespace()
    {
        var query = new RevealSecretQuery("   ");

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_ExpiredOutcome_when_cache_says_known_expired()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretExpiredOutcome>();
        await _repository.DidNotReceive().TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_NotFoundOutcome_when_repository_returns_NotFound()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekNotFoundOutcome());

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _repository.DidNotReceive().ConsumeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_ExpiredOutcome_when_repository_returns_ExpiredOrViewed()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekExpiredOrViewedOutcome());

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretExpiredOutcome>();
        await _repository.DidNotReceive().ConsumeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_non_password_path_returns_ExpiredOutcome_when_TryRevealOnceAsync_returns_ExpiredOrViewed()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, null)));
        _repository.TryRevealOnceAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryRevealExpiredOrViewedOutcome());

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretExpiredOutcome>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_non_password_path_returns_NotFoundOutcome_when_TryRevealOnceAsync_returns_NotFound()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, null)));
        _repository.TryRevealOnceAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryRevealNotFoundOutcome());

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_NotFoundOutcome_when_decrypt_throws()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var secretId = Guid.NewGuid();
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 1, null)));
        _repository.TryRevealOnceAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryRevealSuccessOutcome(MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 1, null)));
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>())
            .Returns(_ => throw new InvalidOperationException("decrypt failed"));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_plaintext_when_all_succeed()
    {
        var tokenBytes = new byte[32];
        Array.Fill(tokenBytes, (byte)7);
        var tokenBase64 = Convert.ToBase64String(tokenBytes);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var secretId = Guid.NewGuid();
        var revealResult = MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 1, null);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(revealResult));
        _repository.TryRevealOnceAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryRevealSuccessOutcome(revealResult));
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes("revealed secret");
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>())
            .Returns(plaintextBytes);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretSuccessOutcome>();
        var success = (RevealSecretSuccessOutcome)result;
        success.Result.Plaintext.Should().Be("revealed secret");
        await _audit.Received(1).PublishRevealedAsync(secretId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().ConsumeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_returns_NotFoundOutcome_when_token_decodes_to_non_32_bytes()
    {
        var shortBase64 = Convert.ToBase64String(new byte[16]);
        var query = new RevealSecretQuery(shortBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_calls_Decrypt_with_stored_KeyVersion()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var secretId = Guid.NewGuid();
        var revealResult = MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 2, null);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(revealResult));
        _repository.TryRevealOnceAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryRevealSuccessOutcome(revealResult));
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), 2)
            .Returns(System.Text.Encoding.UTF8.GetBytes("decrypted"));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretSuccessOutcome>();
        ((RevealSecretSuccessOutcome)result).Result.Plaintext.Should().Be("decrypted");
        _encryption.Received(1).Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), 2);
    }

    [Fact]
    public async Task Handle_accepts_base64url_style_token()
    {
        var tokenBytes = new byte[32];
        new Random(42).NextBytes(tokenBytes);
        var base64 = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_");
        var query = new RevealSecretQuery(base64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var secretId = Guid.NewGuid();
        var revealResult = MakeRevealResult(secretId, Array.Empty<byte>(), new byte[12], 1, null);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(revealResult));
        _repository.TryRevealOnceAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryRevealSuccessOutcome(revealResult));
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>())
            .Returns(System.Text.Encoding.UTF8.GetBytes("ok"));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretSuccessOutcome>();
        ((RevealSecretSuccessOutcome)result).Result.Plaintext.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_password_protected_without_password_returns_NotFoundOutcome()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64, null);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var salt = new byte[16];
        new Random(1).NextBytes(salt);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, salt, true, "dGVzdA==")));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        _encryption.DidNotReceive().Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>());
        await _repository.DidNotReceive().ConsumeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Wrong password must not consume the secret (no ConsumeAsync call).</summary>
    [Fact]
    public async Task Handle_password_protected_with_wrong_password_returns_NotFoundOutcome_and_does_not_consume()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64, "wrong-password");
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var salt = new byte[16];
        new Random(1).NextBytes(salt);
        var correctKey = new byte[32];
        new Random(2).NextBytes(correctKey);
        var storedHashBase64 = Convert.ToBase64String(correctKey);
        _passwordDerivation.DeriveKey(Arg.Any<byte[]>(), Arg.Any<byte[]>(), 32).Returns(new byte[32]); // wrong key
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, salt, true, storedHashBase64)));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        _encryption.DidNotReceive().Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>());
        await _repository.DidNotReceive().ConsumeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_password_protected_with_correct_password_decrypts_and_returns_plaintext()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64, "correct");
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var secretId = Guid.NewGuid();
        var salt = new byte[16];
        new Random(1).NextBytes(salt);
        var derivedKey = new byte[32];
        new Random(2).NextBytes(derivedKey);
        var storedHashBase64 = Convert.ToBase64String(derivedKey);
        _passwordDerivation.DeriveKey(Arg.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "correct"), Arg.Any<byte[]>(), 32).Returns(derivedKey);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 1, salt, true, storedHashBase64)));
        _repository.ConsumeAsync(secretId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>()).Returns(System.Text.Encoding.UTF8.GetBytes("decrypted-secret"));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretSuccessOutcome>();
        ((RevealSecretSuccessOutcome)result).Result.Plaintext.Should().Be("decrypted-secret");
    }

    [Fact]
    public async Task Handle_password_protected_correct_password_but_ConsumeAsync_returns_false_returns_ExpiredOutcome()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64, "correct");
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var secretId = Guid.NewGuid();
        var salt = new byte[16];
        new Random(1).NextBytes(salt);
        var derivedKey = new byte[32];
        new Random(2).NextBytes(derivedKey);
        var storedHashBase64 = Convert.ToBase64String(derivedKey);
        _passwordDerivation.DeriveKey(Arg.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "correct"), Arg.Any<byte[]>(), 32).Returns(derivedKey);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 1, salt, true, storedHashBase64)));
        _repository.ConsumeAsync(secretId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretExpiredOutcome>();
        _encryption.DidNotReceive().Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>());
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Audit must not be called when decrypt throws after TryRevealOnceAsync (view-once path).</summary>
    [Fact]
    public async Task Handle_when_decrypt_throws_does_not_call_audit_PublishRevealedAsync()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var secretId = Guid.NewGuid();
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 1, null)));
        _repository.TryRevealOnceAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryRevealSuccessOutcome(MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 1, null)));
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>())
            .Returns(_ => throw new InvalidOperationException("decrypt failed"));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Password-protected secret with null salt returns NotFound and does not call audit.</summary>
    [Fact]
    public async Task Handle_password_protected_with_null_salt_returns_NotFoundOutcome_and_does_not_call_audit()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64, "any");
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, salt: null, isPasswordProtected: true, "dGVzdA==")));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        _encryption.DidNotReceive().Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>());
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Password-protected secret with null password hash returns NotFound and does not call audit.</summary>
    [Fact]
    public async Task Handle_password_protected_with_null_passwordHashBase64_returns_NotFoundOutcome_and_does_not_call_audit()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64, "any");
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var salt = new byte[16];
        new Random(1).NextBytes(salt);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, salt, isPasswordProtected: true, passwordHashBase64: null)));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        _encryption.DidNotReceive().Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>());
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Leading/trailing space in password is trimmed and compared consistently (create uses Trim).</summary>
    [Fact]
    public async Task Handle_password_protected_with_trimmed_password_matches_create_behavior()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64, "  correct  ");
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var secretId = Guid.NewGuid();
        var salt = new byte[16];
        new Random(1).NextBytes(salt);
        var derivedKey = new byte[32];
        new Random(2).NextBytes(derivedKey);
        var storedHashBase64 = Convert.ToBase64String(derivedKey);
        _passwordDerivation.DeriveKey(Arg.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "correct"), Arg.Any<byte[]>(), 32).Returns(derivedKey);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(secretId, new byte[] { 1 }, new byte[] { 2 }, 1, salt, true, storedHashBase64)));
        _repository.ConsumeAsync(secretId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>()).Returns(System.Text.Encoding.UTF8.GetBytes("decrypted-secret"));

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretSuccessOutcome>();
        ((RevealSecretSuccessOutcome)result).Result.Plaintext.Should().Be("decrypted-secret");
    }

    /// <summary>When cancellation is requested, handler respects it (no audit on cancel).</summary>
    [Fact]
    public async Task Handle_respects_cancellation_and_does_not_call_audit()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                if (callInfo.Arg<CancellationToken>().IsCancellationRequested)
                    throw new OperationCanceledException();
                return Task.FromResult(false);
            });
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, null)));
        _repository.TryRevealOnceAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryRevealSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, null)));
        _encryption.Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>()).Returns(System.Text.Encoding.UTF8.GetBytes("ok"));

        var act = () => _sut.Handle(query, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _audit.DidNotReceive().PublishRevealedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Expiry decision uses utcNow from time provider (cache/repo receive it).</summary>
    [Fact]
    public async Task Handle_passes_utcNow_from_time_provider_to_repository()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64);
        var expectedUtcNow = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        _time.UtcNow.Returns(expectedUtcNow);
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), expectedUtcNow, Arg.Any<CancellationToken>())
            .Returns(new TryPeekNotFoundOutcome());

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _repository.Received(1).TryPeekSecretAsync(Arg.Any<TokenHash>(), expectedUtcNow, Arg.Any<CancellationToken>());
    }

    /// <summary>Invalid base64 token (malformed) returns NotFound without calling repository.</summary>
    [Fact]
    public async Task Handle_returns_NotFoundOutcome_when_token_is_invalid_base64()
    {
        var query = new RevealSecretQuery("not-valid-base64!!!");

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        await _cache.DidNotReceive().IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Password-protected secret with invalid stored PasswordHashBase64 (not base64) returns NotFound after delay.</summary>
    [Fact]
    public async Task Handle_password_protected_with_invalid_stored_hash_base64_returns_NotFoundOutcome()
    {
        var tokenBase64 = Convert.ToBase64String(new byte[32]);
        var query = new RevealSecretQuery(tokenBase64, "any");
        _cache.IsKnownExpiredAsync(Arg.Any<TokenHash>(), Arg.Any<CancellationToken>()).Returns(false);
        var salt = new byte[16];
        new Random(1).NextBytes(salt);
        _repository.TryPeekSecretAsync(Arg.Any<TokenHash>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new TryPeekSuccessOutcome(MakeRevealResult(Guid.NewGuid(), new byte[] { 1 }, new byte[] { 2 }, 1, salt, true, "not-valid-base64!!!")));
        _passwordDerivation.DeriveKey(Arg.Any<byte[]>(), Arg.Any<byte[]>(), 32).Returns(new byte[32]);

        var result = await _sut.Handle(query, CancellationToken.None);

        result.Should().BeOfType<RevealSecretNotFoundOutcome>();
        _encryption.DidNotReceive().Decrypt(Arg.Any<byte[]>(), Arg.Any<byte[]>(), Arg.Any<int>());
        await _repository.DidNotReceive().ConsumeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    /// <summary>TokenHashBase64FromToken returns same hash as TokenHashFromRequest for valid 32-byte base64url token.</summary>
    [Fact]
    public void TokenHashBase64FromToken_valid_32_byte_base64url_returns_matching_base64_hash()
    {
        var tokenBytes = new byte[32];
        new Random(42).NextBytes(tokenBytes);
        var base64Standard = Convert.ToBase64String(tokenBytes);
        var base64Url = base64Standard.Replace("+", "-").Replace("/", "_");

        var hashBase64 = RevealSecretQueryHandler.TokenHashBase64FromToken(base64Url);

        hashBase64.Should().NotBeNullOrEmpty();
        var decoded = Convert.FromBase64String(hashBase64!);
        decoded.Should().HaveCount(32);
        var expectedHash = System.Security.Cryptography.SHA256.HashData(tokenBytes);
        decoded.Should().BeEquivalentTo(expectedHash);
    }

    /// <summary>TokenHashBase64FromToken returns null for invalid token (same contract as TokenHashFromRequest).</summary>
    [Fact]
    public void TokenHashBase64FromToken_invalid_token_returns_null()
    {
        RevealSecretQueryHandler.TokenHashBase64FromToken("not-valid-base64!!!").Should().BeNull();
        RevealSecretQueryHandler.TokenHashBase64FromToken("").Should().BeNull();
        RevealSecretQueryHandler.TokenHashBase64FromToken("   ").Should().BeNull();
        var shortToken = Convert.ToBase64String(new byte[16]);
        RevealSecretQueryHandler.TokenHashBase64FromToken(shortToken).Should().BeNull();
    }

}
