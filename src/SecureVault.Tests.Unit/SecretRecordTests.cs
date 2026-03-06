using FluentAssertions;
using SecureVault.Domain.Entities;
using SecureVault.Domain.ValueObjects;
using Xunit;

namespace SecureVault.Tests.Unit;

public class SecretRecordTests
{
    private static readonly TokenHash SampleHash = TokenHash.FromBytes(new byte[32]);

    [Fact]
    public void Constructor_sets_all_properties()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var expiresAt = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);

        var record = new SecretRecord(id, SampleHash, ExpiryType.OneHour, createdAt, expiresAt, null);

        record.Id.Should().Be(id);
        record.TokenHash.Should().Be(SampleHash);
        record.ExpiryType.Should().Be(ExpiryType.OneHour);
        record.UtcCreatedAt.Should().Be(createdAt);
        record.UtcExpiresAt.Should().Be(expiresAt);
        record.UtcRevealedAt.Should().BeNull();
    }

    [Fact]
    public void IsExpired_returns_true_when_utcNow_after_expiresAt()
    {
        var expiresAt = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var record = new SecretRecord(Guid.NewGuid(), SampleHash, ExpiryType.OneHour,
            expiresAt.AddHours(-1), expiresAt, null);

        record.IsExpired(expiresAt.AddSeconds(1)).Should().BeTrue();
        record.IsExpired(expiresAt).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_returns_false_when_utcNow_before_expiresAt()
    {
        var expiresAt = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var record = new SecretRecord(Guid.NewGuid(), SampleHash, ExpiryType.OneHour,
            expiresAt.AddHours(-1), expiresAt, null);

        record.IsExpired(expiresAt.AddSeconds(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_returns_true_when_utcNow_equals_UtcExpiresAt()
    {
        var expiresAt = new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var record = new SecretRecord(Guid.NewGuid(), SampleHash, ExpiryType.OneHour,
            expiresAt.AddHours(-1), expiresAt, null);

        record.IsExpired(expiresAt).Should().BeTrue("boundary at exact UtcExpiresAt must be treated as expired");
    }

    [Fact]
    public void IsRevealed_returns_true_when_UtcRevealedAt_set()
    {
        var revealedAt = new DateTime(2025, 1, 1, 14, 0, 0, DateTimeKind.Utc);
        var record = new SecretRecord(Guid.NewGuid(), SampleHash, ExpiryType.BurnAfterRead,
            revealedAt.AddHours(-1), revealedAt.AddYears(1), revealedAt);

        record.IsRevealed.Should().BeTrue();
    }

    [Fact]
    public void IsRevealed_returns_false_when_UtcRevealedAt_null()
    {
        var record = new SecretRecord(Guid.NewGuid(), SampleHash, ExpiryType.OneHour,
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null);

        record.IsRevealed.Should().BeFalse();
    }

    [Fact]
    public void CanReveal_returns_true_when_not_revealed_and_not_expired()
    {
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var record = new SecretRecord(Guid.NewGuid(), SampleHash, ExpiryType.OneHour,
            now.AddHours(-1), now.AddHours(1), null);

        record.CanReveal(now).Should().BeTrue();
    }

    [Fact]
    public void CanReveal_returns_false_when_expired()
    {
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var record = new SecretRecord(Guid.NewGuid(), SampleHash, ExpiryType.OneHour,
            now.AddHours(-2), now.AddHours(-1), null);

        record.CanReveal(now).Should().BeFalse();
    }

    [Fact]
    public void CanReveal_returns_false_when_already_revealed()
    {
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var record = new SecretRecord(Guid.NewGuid(), SampleHash, ExpiryType.BurnAfterRead,
            now.AddHours(-1), now.AddYears(1), now.AddMinutes(-5));

        record.CanReveal(now).Should().BeFalse();
    }

    [Fact]
    public void Constructor_throws_when_IsPasswordProtected_true_but_PasswordHash_null()
    {
        var act = () => new SecretRecord(
            Guid.NewGuid(),
            SampleHash,
            ExpiryType.OneHour,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            null,
            isPasswordProtected: true,
            passwordHash: null,
            passwordSalt: "c2FsdA==");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*PasswordHash and PasswordSalt must be set*")
            .Where(e => e.ParamName == "isPasswordProtected");
    }

    [Fact]
    public void Constructor_throws_when_IsPasswordProtected_true_but_PasswordSalt_empty()
    {
        var act = () => new SecretRecord(
            Guid.NewGuid(),
            SampleHash,
            ExpiryType.OneHour,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            null,
            isPasswordProtected: true,
            passwordHash: "dGVzdA==",
            passwordSalt: "");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*PasswordHash and PasswordSalt must be set*");
    }

    [Fact]
    public void Constructor_succeeds_when_IsPasswordProtected_true_and_both_hash_and_salt_set()
    {
        var record = new SecretRecord(
            Guid.NewGuid(),
            SampleHash,
            ExpiryType.OneHour,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            null,
            isPasswordProtected: true,
            passwordHash: "dGVzdA==",
            passwordSalt: "c2FsdA==");

        record.IsPasswordProtected.Should().BeTrue();
        record.PasswordHash.Should().Be("dGVzdA==");
        record.PasswordSalt.Should().Be("c2FsdA==");
    }
}
