using FluentAssertions;
using NSubstitute;
using StackExchange.Redis;
using SecureVault.Infrastructure.RateLimiting;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class RedisRateLimitBackendTests
{
    [Fact]
    public async Task IncrementAndGetAsync_returns_count_from_StringIncrementAsync()
    {
        var db = Substitute.For<IDatabase>();
        db.StringIncrementAsync(Arg.Any<RedisKey>()).Returns(42L);
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        var sut = new RedisRateLimitBackend(redis);
        var windowTtl = TimeSpan.FromMinutes(1);

        var count = await sut.IncrementAndGetAsync("rate:create:client1", windowTtl, CancellationToken.None);

        count.Should().Be(42L);
        await db.Received(1).StringIncrementAsync(Arg.Any<RedisKey>());
    }

    [Fact]
    public async Task IncrementAndGetAsync_when_count_is_1_calls_KeyExpireAsync_with_window_ttl()
    {
        var db = Substitute.For<IDatabase>();
        db.StringIncrementAsync(Arg.Any<RedisKey>()).Returns(1L);
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        var sut = new RedisRateLimitBackend(redis);
        var windowTtl = TimeSpan.FromMinutes(5);

        await sut.IncrementAndGetAsync("rate:reveal:client1", windowTtl, CancellationToken.None);

        await db.Received(1).KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Is<TimeSpan?>(t => t == windowTtl));
    }

    [Fact]
    public async Task IncrementAndGetAsync_when_count_is_greater_than_1_does_not_call_KeyExpireAsync_again()
    {
        var db = Substitute.For<IDatabase>();
        db.StringIncrementAsync(Arg.Any<RedisKey>()).Returns(2L);
        var redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);
        var sut = new RedisRateLimitBackend(redis);
        var windowTtl = TimeSpan.FromMinutes(1);

        await sut.IncrementAndGetAsync("rate:create:client1", windowTtl, CancellationToken.None);

        await db.Received(1).StringIncrementAsync(Arg.Any<RedisKey>());
        await db.DidNotReceive().KeyExpireAsync(Arg.Any<RedisKey>(), Arg.Any<TimeSpan?>());
    }
}
