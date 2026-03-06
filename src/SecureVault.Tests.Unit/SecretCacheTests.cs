using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;
using SecureVault.Domain.ValueObjects;
using SecureVault.Infrastructure.Caching;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class SecretCacheTests
{
    private static readonly TokenHash SampleHash = TokenHash.FromBytes(new byte[32]);

    [Fact]
    public async Task SetSecretTtlAsync_calls_cache_SetAsync_with_correct_key_and_ttl()
    {
        var cache = Substitute.For<IDistributedCache>();
        var sut = new SecretCache(cache);
        var ttl = TimeSpan.FromMinutes(5);
        var expectedKey = "secret:ttl:" + SampleHash.ToBase64();

        await sut.SetSecretTtlAsync(SampleHash, ttl, CancellationToken.None);

        await cache.Received(1).SetAsync(
            expectedKey,
            Arg.Is<byte[]>(b => b != null && b.Length == 1 && b[0] == 1),
            Arg.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == ttl),
            CancellationToken.None);
    }

    [Fact]
    public async Task IsKnownExpiredAsync_returns_false_and_calls_GetAsync_with_correct_key()
    {
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        var sut = new SecretCache(cache);
        var expectedKey = "secret:ttl:" + SampleHash.ToBase64();

        var result = await sut.IsKnownExpiredAsync(SampleHash, CancellationToken.None);

        result.Should().BeFalse();
        await cache.Received(1).GetAsync(expectedKey, CancellationToken.None);
    }

    [Fact]
    public async Task SetSecretTtlAsync_passes_cancellation_token_to_cache()
    {
        var cache = Substitute.For<IDistributedCache>();
        var sut = new SecretCache(cache);
        var cts = new CancellationTokenSource();

        await sut.SetSecretTtlAsync(SampleHash, TimeSpan.FromMinutes(1), cts.Token);

        await cache.Received(1).SetAsync(
            Arg.Any<string>(),
            Arg.Any<byte[]>(),
            Arg.Any<DistributedCacheEntryOptions>(),
            cts.Token);
    }
}
