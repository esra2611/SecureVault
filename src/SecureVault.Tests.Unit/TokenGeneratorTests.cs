using FluentAssertions;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Infrastructure.Crypto;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class TokenGeneratorTests
{
    private readonly ITokenGenerator _sut = new TokenGenerator();

    [Fact]
    public void Generate_returns_32_byte_token()
    {
        var (tokenBytes, hash) = _sut.Generate();

        tokenBytes.Should().HaveCount(32, "token must be 256 bits for sufficient entropy");
        hash.Value.Should().HaveCount(32, "SHA-256 hash is 32 bytes");
    }

    [Fact]
    public void Generate_produces_different_tokens_each_call()
    {
        var (t1, h1) = _sut.Generate();
        var (t2, h2) = _sut.Generate();

        t1.Should().NotBeEquivalentTo(t2);
        h1.Value.Should().NotBeEquivalentTo(h2.Value);
    }

    [Fact]
    public void Hash_equals_SHA256_of_token_bytes()
    {
        var (tokenBytes, hash) = _sut.Generate();
        var expectedHash = System.Security.Cryptography.SHA256.HashData(tokenBytes);

        hash.Value.Should().BeEquivalentTo(expectedHash);
    }

    [Fact]
    public void Generate_produces_unique_hashes_over_many_calls()
    {
        const int count = 500;
        var hashes = new List<byte[]>();
        for (var i = 0; i < count; i++)
        {
            var (_, hash) = _sut.Generate();
            hashes.Add(hash.Value.ToArray());
        }
        var distinct = hashes.Select(h => Convert.ToBase64String(h)).Distinct().Count();
        distinct.Should().Be(count, "all generated token hashes must be unique");
    }
}
