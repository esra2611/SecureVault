using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureVault.Infrastructure.Crypto;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class Argon2KeyDerivationTests
{
    [Fact]
    public void DeriveKey_same_password_and_salt_produces_same_key()
    {
        var options = Options.Create(new Argon2Options { Iterations = 2, MemorySizeKb = 1024, DegreeOfParallelism = 1 });
        var sut = new Argon2KeyDerivation(options);
        var password = System.Text.Encoding.UTF8.GetBytes("password");
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var key1 = sut.DeriveKey(password, salt, 32);
        var key2 = sut.DeriveKey(password, salt, 32);

        key1.Should().BeEquivalentTo(key2);
        key1.Should().HaveCount(32);
    }

    [Fact]
    public void DeriveKey_different_salt_produces_different_key()
    {
        var options = Options.Create(new Argon2Options { Iterations = 2, MemorySizeKb = 1024, DegreeOfParallelism = 1 });
        var sut = new Argon2KeyDerivation(options);
        var password = System.Text.Encoding.UTF8.GetBytes("password");
        var salt1 = new byte[16];
        var salt2 = new byte[16];
        salt1[0] = 1;
        salt2[0] = 2;

        var key1 = sut.DeriveKey(password, salt1, 32);
        var key2 = sut.DeriveKey(password, salt2, 32);

        key1.Should().NotBeEquivalentTo(key2);
    }

    /// <summary>Constructor clamps Iterations below MinIterations; DeriveKey still uses clamped value.</summary>
    [Fact]
    public void Constructor_clamps_iterations_below_min_and_derivation_succeeds()
    {
        var options = Options.Create(new Argon2Options { Iterations = 0, MemorySizeKb = 1024, DegreeOfParallelism = 1 });
        var sut = new Argon2KeyDerivation(options);
        var password = System.Text.Encoding.UTF8.GetBytes("pwd");
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var key = sut.DeriveKey(password, salt, 32);

        key.Should().HaveCount(32);
        key.Should().NotBeNullOrEmpty();
    }

    /// <summary>Constructor clamps MemorySizeKb below MinMemoryKb; DeriveKey still uses clamped value.</summary>
    [Fact]
    public void Constructor_clamps_memory_below_min_and_derivation_succeeds()
    {
        var options = Options.Create(new Argon2Options { Iterations = 2, MemorySizeKb = 0, DegreeOfParallelism = 1 });
        var sut = new Argon2KeyDerivation(options);
        var password = System.Text.Encoding.UTF8.GetBytes("pwd");
        var salt = new byte[16];
        new Random(1).NextBytes(salt);

        var key = sut.DeriveKey(password, salt, 32);

        key.Should().HaveCount(32);
    }
}
