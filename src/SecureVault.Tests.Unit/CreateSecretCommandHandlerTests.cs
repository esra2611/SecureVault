using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Application.Common.Models;
using SecureVault.Application.Secrets.CreateSecret;
using SecureVault.Domain.ValueObjects;
using Xunit;

namespace SecureVault.Tests.Unit;

public class CreateSecretCommandHandlerTests
{
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
    private readonly CreateSecretCommandHandler _sut;

    private static readonly DateTime FixedUtc = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] TokenBytes = new byte[32];
    private static readonly TokenHash TokenHash = TokenHash.FromBytes(new byte[32]);

    public CreateSecretCommandHandlerTests()
    {
        _repository = Substitute.For<ISecretRepository>();
        _encryption = Substitute.For<IEncryptionService>();
        _keyProvider = Substitute.For<IKeyProvider>();
        _keyProvider.GetCurrentVersion().Returns(1);
        _tokenGenerator = Substitute.For<ITokenGenerator>();
        _cache = Substitute.For<ISecretCache>();
        _audit = Substitute.For<IAuditPublisher>();
        _time = Substitute.For<ITimeProvider>();
        _linkBuilder = Substitute.For<ICreateSecretLinkBuilder>();
        _expiryConfig = Substitute.For<ISecretExpiryConfig>();
        _expiryConfig.OverrideTtlForTests.Returns((TimeSpan?)null);
        _passwordDerivation = Substitute.For<IPasswordDerivation>();
        _logger = Substitute.For<ILogger<CreateSecretCommandHandler>>();

        _time.UtcNow.Returns(FixedUtc);
        _tokenGenerator.Generate().Returns((TokenBytes, TokenHash));
        _encryption.Encrypt(Arg.Any<byte[]>())
            .Returns((new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 }));
        _repository.AddAsync(Arg.Any<AddSecretRequest>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());
        _linkBuilder.Build(Arg.Any<byte[]>()).Returns("https://example.com/s/abc");
        var derivedKey = new byte[32];
        new Random(1).NextBytes(derivedKey);
        _passwordDerivation.DeriveKey(Arg.Any<byte[]>(), Arg.Any<byte[]>(), 32).Returns(derivedKey);

        _sut = new CreateSecretCommandHandler(_repository, _encryption, _keyProvider, _tokenGenerator, _cache, _audit, _time, _linkBuilder, _expiryConfig, _passwordDerivation, _logger);
    }

    [Fact]
    public async Task Handle_returns_share_url_and_token_id_hint()
    {
        var command = new CreateSecretCommand("my secret", ExpiryType.OneHour, null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.ShareUrl.Should().Be("https://example.com/s/abc");
        result.TokenIdHint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_calls_repository_AddAsync_with_correct_expiry_for_OneHour()
    {
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r =>
                r.ExpiryType == ExpiryType.OneHour &&
                r.UtcExpiresAt == FixedUtc.AddHours(1) &&
                !r.IsPasswordProtected &&
                r.PasswordHashBase64 == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_calls_repository_AddAsync_with_correct_expiry_for_TwentyFourHours()
    {
        var command = new CreateSecretCommand("x", ExpiryType.TwentyFourHours, null);

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r =>
                r.ExpiryType == ExpiryType.TwentyFourHours &&
                r.UtcExpiresAt == FixedUtc.AddHours(24) &&
                !r.IsPasswordProtected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_calls_repository_AddAsync_with_correct_expiry_for_SevenDays()
    {
        var command = new CreateSecretCommand("x", ExpiryType.SevenDays, null);

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r =>
                r.ExpiryType == ExpiryType.SevenDays &&
                r.UtcExpiresAt == FixedUtc.AddDays(7) &&
                !r.IsPasswordProtected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_calls_repository_AddAsync_with_long_expiry_for_BurnAfterRead()
    {
        var command = new CreateSecretCommand("x", ExpiryType.BurnAfterRead, null);

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r =>
                r.ExpiryType == ExpiryType.BurnAfterRead &&
                r.UtcExpiresAt == FixedUtc.AddYears(10) &&
                !r.IsPasswordProtected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_calls_cache_SetSecretTtlAsync_when_expiry_in_future()
    {
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        await _sut.Handle(command, CancellationToken.None);

        await _cache.Received(1).SetSecretTtlAsync(Arg.Any<TokenHash>(), TimeSpan.FromHours(1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_calls_audit_PublishCreatedAsync()
    {
        var id = Guid.NewGuid();
        _repository.AddAsync(Arg.Any<AddSecretRequest>(), Arg.Any<CancellationToken>())
            .Returns(id);
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        await _sut.Handle(command, CancellationToken.None);

        await _audit.Received(1).PublishCreatedAsync(id, Arg.Any<string>(), FixedUtc.AddHours(1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_calls_linkBuilder_Build_with_token_bytes()
    {
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        await _sut.Handle(command, CancellationToken.None);

        _linkBuilder.Received(1).Build(TokenBytes);
    }

    [Fact]
    public async Task Handle_uses_OverrideTtlForTests_when_set_for_non_burn_expiry()
    {
        _expiryConfig.OverrideTtlForTests.Returns(TimeSpan.FromSeconds(3));
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r =>
                r.ExpiryType == ExpiryType.OneHour &&
                r.UtcExpiresAt == FixedUtc.AddSeconds(3) &&
                !r.IsPasswordProtected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_with_password_derives_verification_hash_stores_it_and_calls_Encrypt_with_master_key()
    {
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, "mypassword");

        await _sut.Handle(command, CancellationToken.None);

        _passwordDerivation.Received(1).DeriveKey(
            Arg.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "mypassword"),
            Arg.Is<byte[]>(s => s != null && s.Length == 16),
            32);
        _encryption.Received(1).Encrypt(Arg.Any<byte[]>());
        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r =>
                r.SaltForPassword != null &&
                r.IsPasswordProtected &&
                r.PasswordHashBase64 != null),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Ensures key versioning is wired: repository receives keyVersion from IKeyProvider.GetCurrentVersion().</summary>
    [Fact]
    public async Task Handle_calls_AddAsync_with_keyVersion_from_GetCurrentVersion()
    {
        _keyProvider.GetCurrentVersion().Returns(2);
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r => r.KeyVersion == 2 && !r.IsPasswordProtected),
            Arg.Any<CancellationToken>());
    }

    /// <summary>When repository AddAsync throws, audit is not called.</summary>
    [Fact]
    public async Task Handle_when_AddAsync_throws_does_not_call_audit_PublishCreatedAsync()
    {
        _repository.AddAsync(Arg.Any<AddSecretRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => Task.FromException<Guid>(new InvalidOperationException("db failure")));
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _audit.DidNotReceive().PublishCreatedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    /// <summary>When cancellation is requested (e.g. repo throws OperationCanceledException), audit is not called.</summary>
    [Fact]
    public async Task Handle_when_AddAsync_throws_OperationCanceledException_does_not_call_audit()
    {
        _repository.AddAsync(Arg.Any<AddSecretRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => Task.FromException<Guid>(new OperationCanceledException()));
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _audit.DidNotReceive().PublishCreatedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Expiry uses utcNow from time provider.</summary>
    [Fact]
    public async Task Handle_uses_utcNow_from_time_provider_for_expiresAt()
    {
        var expectedUtcNow = new DateTime(2025, 6, 10, 14, 0, 0, DateTimeKind.Utc);
        _time.UtcNow.Returns(expectedUtcNow);
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r =>
                r.ExpiryType == ExpiryType.OneHour &&
                r.UtcExpiresAt == expectedUtcNow.AddHours(1) &&
                !r.IsPasswordProtected),
            Arg.Any<CancellationToken>());
    }

    /// <summary>When cache SetSecretTtlAsync throws, handler still returns success (best-effort cache; DB is source of truth).</summary>
    [Fact]
    public async Task Handle_when_cache_SetSecretTtlAsync_throws_still_returns_success()
    {
        _cache.SetSecretTtlAsync(Arg.Any<TokenHash>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("cache unavailable"));
        var command = new CreateSecretCommand("x", ExpiryType.OneHour, null);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.ShareUrl.Should().Be("https://example.com/s/abc");
        await _repository.Received(1).AddAsync(Arg.Any<AddSecretRequest>(), Arg.Any<CancellationToken>());
        await _audit.Received(1).PublishCreatedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Unknown ExpiryType value falls back to default 24h expiry.</summary>
    [Fact]
    public async Task Handle_uses_default_24h_expiry_when_ExpiryType_out_of_enum_range()
    {
        var command = new CreateSecretCommand("x", (ExpiryType)99, null);

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<AddSecretRequest>(r =>
                r.UtcExpiresAt == FixedUtc.AddHours(24)),
            Arg.Any<CancellationToken>());
    }
}
