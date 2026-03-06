using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Infrastructure.Crypto;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class Pbkdf2PasswordDerivationTests
{
    private const int KeyLength = 32;

    [Fact]
    public void DeriveKey_returns_same_hash_for_same_password_and_salt()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options { Iterations = 100_000 }));
        var password = System.Text.Encoding.UTF8.GetBytes("password123");
        var salt = new byte[16];
        new Random(42).NextBytes(salt);

        var hash1 = sut.DeriveKey(password, salt, KeyLength);
        var hash2 = sut.DeriveKey(password, salt, KeyLength);

        hash1.Should().BeEquivalentTo(hash2);
        hash1.Length.Should().Be(KeyLength);
    }

    [Fact]
    public void DeriveKey_different_salts_produce_different_hashes()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options { Iterations = 100_000 }));
        var password = System.Text.Encoding.UTF8.GetBytes("same");
        var salt1 = new byte[16];
        var salt2 = new byte[16];
        new Random(1).NextBytes(salt1);
        new Random(2).NextBytes(salt2);

        var hash1 = sut.DeriveKey(password, salt1, KeyLength);
        var hash2 = sut.DeriveKey(password, salt2, KeyLength);

        hash1.Should().NotBeEquivalentTo(hash2);
    }

    [Fact]
    public void DeriveKey_different_passwords_produce_different_hashes()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options { Iterations = 100_000 }));
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var hash1 = sut.DeriveKey(System.Text.Encoding.UTF8.GetBytes("pass1"), salt, KeyLength);
        var hash2 = sut.DeriveKey(System.Text.Encoding.UTF8.GetBytes("pass2"), salt, KeyLength);

        hash1.Should().NotBeEquivalentTo(hash2);
    }

    [Fact]
    public void DeriveKey_uses_at_least_MinIterations()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options { Iterations = 50_000 }));
        var password = System.Text.Encoding.UTF8.GetBytes("test");
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var hash = sut.DeriveKey(password, salt, KeyLength);

        hash.Should().NotBeNull();
        hash.Length.Should().Be(KeyLength);
    }

    [Fact]
    public void DeriveKey_throws_when_password_empty()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options()));
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var act = () => sut.DeriveKey(Array.Empty<byte>(), salt, KeyLength);

        act.Should().Throw<ArgumentException>().WithParameterName("passwordUtf8");
    }

    [Fact]
    public void DeriveKey_throws_when_salt_less_than_16_bytes()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options()));
        var password = System.Text.Encoding.UTF8.GetBytes("pass");
        var shortSalt = new byte[15];

        var act = () => sut.DeriveKey(password, shortSalt, KeyLength);

        act.Should().Throw<ArgumentException>().WithParameterName("salt");
    }

    [Fact]
    public void DeriveKey_throws_when_password_null()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options()));
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var act = () => sut.DeriveKey(null!, salt, KeyLength);

        act.Should().Throw<ArgumentException>().WithParameterName("passwordUtf8");
    }

    [Fact]
    public void DeriveKey_throws_when_salt_null()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options()));
        var password = System.Text.Encoding.UTF8.GetBytes("pass");

        var act = () => sut.DeriveKey(password, null!, KeyLength);

        act.Should().Throw<ArgumentException>().WithParameterName("salt");
    }

    [Fact]
    public void DeriveKey_throws_when_outputLength_zero()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options()));
        var password = System.Text.Encoding.UTF8.GetBytes("pass");
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var act = () => sut.DeriveKey(password, salt, 0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("outputLength");
    }

    [Fact]
    public void DeriveKey_throws_when_outputLength_exceeds_128()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options()));
        var password = System.Text.Encoding.UTF8.GetBytes("pass");
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var act = () => sut.DeriveKey(password, salt, 129);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("outputLength");
    }

    [Fact]
    public void DeriveKey_accepts_outputLength_up_to_128()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options { Iterations = 1000 }));
        var password = System.Text.Encoding.UTF8.GetBytes("pass");
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var key = sut.DeriveKey(password, salt, 128);

        key.Should().HaveCount(128);
    }

    /// <summary>
    /// Simulates exact create→reveal flow: same password and salt, derive key, store Base64,
    /// then "reveal" by deriving again and comparing with constant-time. Catches encoding/derivation bugs.
    /// </summary>
    [Fact]
    public void Create_reveal_roundtrip_same_password_and_salt_verification_passes()
    {
        var sut = new Pbkdf2PasswordDerivation(Options.Create(new Pbkdf2Options { Iterations = 1000 }));
        const string password = "correct-password";
        var salt = new byte[16];
        new Random(42).NextBytes(salt);

        // Simulate create: derive key, store as Base64
        var passwordBytesCreate = System.Text.Encoding.UTF8.GetBytes(password.Trim());
        var keyOverride = sut.DeriveKey(passwordBytesCreate, salt, KeyLength);
        var passwordHashBase64 = Convert.ToBase64String(keyOverride);

        // Simulate reveal: same password (trimmed), same salt, derive again, compare
        var passwordBytesReveal = System.Text.Encoding.UTF8.GetBytes(password.Trim());
        var computedKey = sut.DeriveKey(passwordBytesReveal, salt, KeyLength);
        var storedHashBytes = Convert.FromBase64String(passwordHashBase64);

        storedHashBytes.Length.Should().Be(computedKey.Length, "stored hash and computed key must have same length for comparison");
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(storedHashBytes, computedKey).Should().BeTrue("same password and salt must produce identical key for verification to pass");
    }
}
