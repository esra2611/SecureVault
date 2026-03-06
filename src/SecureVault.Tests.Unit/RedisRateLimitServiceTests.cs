using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SecureVault.Infrastructure.RateLimiting;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class RedisRateLimitServiceTests
{
    private static RedisRateLimitService CreateSut(IRateLimitBackend backend, int createPerWindow = 3, int revealPerWindow = 2, int windowSeconds = 60)
    {
        var options = Options.Create(new RedisRateLimitOptions
        {
            CreateSecretPerWindow = createPerWindow,
            RevealPerWindow = revealPerWindow,
            WindowSeconds = windowSeconds
        });
        return new RedisRateLimitService(backend, options);
    }

    private static InMemoryRateLimitBackend CreateBackend() => new();

    [Fact]
    public async Task TryAcquireAsync_create_allows_up_to_max_then_denies()
    {
        var backend = CreateBackend();
        var sut = CreateSut(backend, createPerWindow: 3);

        var r1 = await sut.TryAcquireAsync("create", "client1");
        var r2 = await sut.TryAcquireAsync("create", "client1");
        var r3 = await sut.TryAcquireAsync("create", "client1");
        var r4 = await sut.TryAcquireAsync("create", "client1");

        r1.Should().BeTrue();
        r2.Should().BeTrue();
        r3.Should().BeTrue();
        r4.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_reveal_allows_up_to_max_then_denies()
    {
        var backend = CreateBackend();
        var sut = CreateSut(backend, revealPerWindow: 2);

        var r1 = await sut.TryAcquireAsync("reveal", "client1");
        var r2 = await sut.TryAcquireAsync("reveal", "client1");
        var r3 = await sut.TryAcquireAsync("reveal", "client1");

        r1.Should().BeTrue();
        r2.Should().BeTrue();
        r3.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_unknown_endpoint_uses_default_limit()
    {
        var backend = CreateBackend();
        var sut = CreateSut(backend);

        for (var i = 0; i < 100; i++)
        {
            var allowed = await sut.TryAcquireAsync("unknown", "client1");
            allowed.Should().BeTrue($"request {i + 1} should be allowed (default 100 per window)");
        }
        var denied = await sut.TryAcquireAsync("unknown", "client1");
        denied.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_different_clients_have_separate_limits()
    {
        var backend = CreateBackend();
        var sut = CreateSut(backend, createPerWindow: 2);

        await sut.TryAcquireAsync("create", "client1");
        await sut.TryAcquireAsync("create", "client1");
        var client1Denied = await sut.TryAcquireAsync("create", "client1");
        var client2Allowed = await sut.TryAcquireAsync("create", "client2");

        client1Denied.Should().BeFalse();
        client2Allowed.Should().BeTrue();
    }

    /// <summary>
    /// In-memory backend for unit tests (atomic increment per key with TTL ignored).
    /// </summary>
    private sealed class InMemoryRateLimitBackend : IRateLimitBackend
    {
        private readonly ConcurrentDictionary<string, long> _counts = new();

        public Task<long> IncrementAndGetAsync(string key, TimeSpan windowTtl, CancellationToken cancellationToken = default)
        {
            var count = _counts.AddOrUpdate(key, 1, (_, v) => v + 1);
            return Task.FromResult(count);
        }
    }
}
