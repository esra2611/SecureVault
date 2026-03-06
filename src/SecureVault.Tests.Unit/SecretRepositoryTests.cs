using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Application.Common.Models;
using SecureVault.Domain.ValueObjects;
using SecureVault.Infrastructure.Persistence;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class SecretRepositoryTests
{
    private static async Task<(SecretRepository repo, SecretVaultDbContext db)> CreateRepositoryAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SecretVaultDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new SecretVaultDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var repo = new SecretRepository(db);
        return (repo, db);
    }

    private static TokenHash SampleTokenHash => TokenHash.FromBytes(new byte[32]);
    private static AddSecretRequest NewRequest(DateTime? utcExpiresAt = null) => new()
    {
        TokenHash = SampleTokenHash,
        ExpiryType = ExpiryType.OneHour,
        UtcExpiresAt = utcExpiresAt ?? DateTime.UtcNow.AddHours(1),
        Ciphertext = [1, 2, 3],
        Nonce = [4, 5, 6],
        KeyVersion = 1,
        SaltForPassword = null,
        IsPasswordProtected = false,
        PasswordHashBase64 = null
    };

    [Fact]
    public async Task AddAsync_persists_entity_and_returns_id()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest();
        var id = await sut.AddAsync(request, CancellationToken.None);
        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task TryPeekSecretAsync_returns_Success_when_secret_exists_and_not_expired()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest(DateTime.UtcNow.AddHours(1));
        var id = await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var outcome = await sut.TryPeekSecretAsync(request.TokenHash, utcNow, CancellationToken.None);

        outcome.Should().BeOfType<TryPeekSuccessOutcome>();
        var success = (TryPeekSuccessOutcome)outcome;
        success.Result.SecretId.Should().Be(id);
        success.Result.Ciphertext.Should().BeEquivalentTo(request.Ciphertext);
        success.Result.Nonce.Should().BeEquivalentTo(request.Nonce);
        success.Result.KeyVersion.Should().Be(1);
    }

    [Fact]
    public async Task TryPeekSecretAsync_returns_NotFound_when_token_hash_does_not_exist()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var unknownHash = TokenHash.FromBytes(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        var utcNow = DateTime.UtcNow;

        var outcome = await sut.TryPeekSecretAsync(unknownHash, utcNow, CancellationToken.None);

        outcome.Should().BeOfType<TryPeekNotFoundOutcome>();
    }

    [Fact]
    public async Task TryPeekSecretAsync_returns_ExpiredOrViewed_when_secret_expired()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest(DateTime.UtcNow.AddSeconds(-1));
        await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var outcome = await sut.TryPeekSecretAsync(request.TokenHash, utcNow, CancellationToken.None);

        outcome.Should().BeOfType<TryPeekExpiredOrViewedOutcome>();
    }

    [Fact]
    public async Task ConsumeAsync_returns_true_and_clears_ciphertext_when_secret_exists()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest();
        var id = await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var consumed = await sut.ConsumeAsync(id, utcNow, CancellationToken.None);

        consumed.Should().BeTrue();
        var peekAfter = await sut.TryPeekSecretAsync(request.TokenHash, utcNow, CancellationToken.None);
        peekAfter.Should().BeOfType<TryPeekExpiredOrViewedOutcome>();
    }

    [Fact]
    public async Task ConsumeAsync_returns_false_when_secret_already_consumed()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest();
        var id = await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;
        await sut.ConsumeAsync(id, utcNow, CancellationToken.None);

        var consumedAgain = await sut.ConsumeAsync(id, utcNow, CancellationToken.None);

        consumedAgain.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAndNotExpiredAsync_returns_true_when_secret_exists_and_not_expired()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest();
        await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var exists = await sut.ExistsAndNotExpiredAsync(request.TokenHash, utcNow, CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAndNotExpiredAsync_returns_false_when_secret_expired()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest(DateTime.UtcNow.AddSeconds(-1));
        await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var exists = await sut.ExistsAndNotExpiredAsync(request.TokenHash, utcNow, CancellationToken.None);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAndNotExpiredAsync_returns_false_when_token_unknown()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var unknownHash = TokenHash.FromBytes(Enumerable.Range(0, 32).Select(i => (byte)(i + 100)).ToArray());
        var utcNow = DateTime.UtcNow;

        var exists = await sut.ExistsAndNotExpiredAsync(unknownHash, utcNow, CancellationToken.None);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTerminalRowsAsync_deletes_expired_and_revealed_rows()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var expiredRequest = NewRequest(DateTime.UtcNow.AddSeconds(-1));
        var idExpired = await sut.AddAsync(expiredRequest, CancellationToken.None);
        var utcNow = DateTime.UtcNow;
        await sut.ConsumeAsync(idExpired, utcNow, CancellationToken.None);

        var deleted = await sut.DeleteTerminalRowsAsync(utcNow, CancellationToken.None);

        deleted.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DeleteTerminalRowsAsync_returns_zero_when_no_rows_to_delete()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest();
        await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var deleted = await sut.DeleteTerminalRowsAsync(utcNow, CancellationToken.None);

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task AddAsync_with_password_protected_stores_salt_and_hash()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var salt = new byte[16];
        new Random(1).NextBytes(salt);
        var request = new AddSecretRequest
        {
            TokenHash = SampleTokenHash,
            ExpiryType = ExpiryType.OneHour,
            UtcExpiresAt = DateTime.UtcNow.AddHours(1),
            Ciphertext = [1, 2, 3],
            Nonce = [4, 5, 6],
            KeyVersion = 1,
            SaltForPassword = salt,
            IsPasswordProtected = true,
            PasswordHashBase64 = Convert.ToBase64String(new byte[32])
        };
        await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var outcome = await sut.TryPeekSecretAsync(request.TokenHash, utcNow, CancellationToken.None);

        outcome.Should().BeOfType<TryPeekSuccessOutcome>();
        var success = (TryPeekSuccessOutcome)outcome;
        success.Result.IsPasswordProtected.Should().BeTrue();
        success.Result.SaltForPassword.Should().BeEquivalentTo(salt);
        success.Result.PasswordHashBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TryRevealOnceAsync_returns_Success_and_consumes_row_when_secret_exists_and_not_expired()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest(DateTime.UtcNow.AddHours(1));
        var id = await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var outcome = await sut.TryRevealOnceAsync(request.TokenHash, utcNow, CancellationToken.None);

        outcome.Should().BeOfType<TryRevealSuccessOutcome>();
        var success = (TryRevealSuccessOutcome)outcome;
        success.Result.SecretId.Should().Be(id);
        success.Result.Ciphertext.Should().BeEquivalentTo(request.Ciphertext);
        success.Result.Nonce.Should().BeEquivalentTo(request.Nonce);
        success.Result.KeyVersion.Should().Be(1);
        // Row was deleted by DELETE...RETURNING; second call finds no row -> NotFound
        var secondCall = await sut.TryRevealOnceAsync(request.TokenHash, utcNow, CancellationToken.None);
        secondCall.Should().BeOfType<TryRevealNotFoundOutcome>();
    }

    [Fact]
    public async Task TryRevealOnceAsync_returns_NotFound_when_token_hash_does_not_exist()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var unknownHash = TokenHash.FromBytes(Enumerable.Range(0, 32).Select(i => (byte)(i + 50)).ToArray());
        var utcNow = DateTime.UtcNow;

        var outcome = await sut.TryRevealOnceAsync(unknownHash, utcNow, CancellationToken.None);

        outcome.Should().BeOfType<TryRevealNotFoundOutcome>();
    }

    [Fact]
    public async Task TryRevealOnceAsync_returns_ExpiredOrViewed_when_secret_expired()
    {
        var (sut, _) = await CreateRepositoryAsync();
        var request = NewRequest(DateTime.UtcNow.AddSeconds(-1));
        await sut.AddAsync(request, CancellationToken.None);
        var utcNow = DateTime.UtcNow;

        var outcome = await sut.TryRevealOnceAsync(request.TokenHash, utcNow, CancellationToken.None);

        outcome.Should().BeOfType<TryRevealExpiredOrViewedOutcome>();
    }
}
