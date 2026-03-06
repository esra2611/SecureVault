using FluentAssertions;
using SecureVault.Domain.ValueObjects;
using Xunit;

namespace SecureVault.Tests.Unit;

public class TokenHashTests
{
    [Fact]
    public void FromBytes_creates_hash_with_32_bytes()
    {
        var bytes = new byte[32];
        Array.Fill(bytes, (byte)1);
        var hash = TokenHash.FromBytes(bytes);
        hash.Value.Should().HaveCount(32);
        hash.Value.Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public void FromBytes_throws_when_not_32_bytes()
    {
        var act = () => TokenHash.FromBytes(new byte[16]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToBase64_roundtrips_with_FromBase64()
    {
        var bytes = new byte[32];
        new Random(42).NextBytes(bytes);
        var hash = TokenHash.FromBytes(bytes);
        var b64 = hash.ToBase64();
        var roundTrip = TokenHash.FromBase64(b64);
        roundTrip.Value.Should().BeEquivalentTo(hash.Value);
    }

    [Fact]
    public void Equals_returns_true_for_same_value()
    {
        var bytes = new byte[32];
        Array.Fill(bytes, (byte)5);
        var a = TokenHash.FromBytes(bytes);
        var b = TokenHash.FromBytes(bytes);
        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void FromBytes_throws_when_null()
    {
        var act = () => TokenHash.FromBytes(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equals_returns_false_when_other_null()
    {
        var hash = TokenHash.FromBytes(new byte[32]);
        hash.Equals(null).Should().BeFalse();
        hash!.Equals((TokenHash?)null).Should().BeFalse();
    }

    [Fact]
    public void Equals_returns_false_when_object_not_TokenHash()
    {
        var hash = TokenHash.FromBytes(new byte[32]);
        hash.Equals(new object()).Should().BeFalse();
    }

    [Fact]
    public void FromBase64_throws_when_invalid_base64()
    {
        var act = () => TokenHash.FromBase64("not-valid-base64!!!");
        act.Should().Throw<FormatException>();
    }
}
