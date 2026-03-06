using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Infrastructure.Jobs;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class SecretCleanupHostedServiceTests
{
    [Fact]
    public void Constructor_accepts_scope_factory_time_and_logger()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var time = Substitute.For<ITimeProvider>();
        time.UtcNow.Returns(DateTime.UtcNow);
        var logger = Substitute.For<ILogger<SecretCleanupHostedService>>();

        var act = () => new SecretCleanupHostedService(scopeFactory, time, logger);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExecuteAsync_exits_when_cancellation_requested_immediately()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var time = Substitute.For<ITimeProvider>();
        time.UtcNow.Returns(DateTime.UtcNow);
        var logger = Substitute.For<ILogger<SecretCleanupHostedService>>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sut = new SecretCleanupHostedService(scopeFactory, time, logger);

        await sut.StartAsync(cts.Token);
        await sut.StopAsync(CancellationToken.None);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Fact]
    public async Task ExecuteAsync_calls_repository_DeleteTerminalRowsAsync_when_scope_provides_repo()
    {
        var repo = Substitute.For<ISecretRepository>();
        repo.DeleteTerminalRowsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(new ServiceCollection().AddSingleton(repo).BuildServiceProvider());
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        var time = Substitute.For<ITimeProvider>();
        var utcNow = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        time.UtcNow.Returns(utcNow);
        var logger = Substitute.For<ILogger<SecretCleanupHostedService>>();
        using var cts = new CancellationTokenSource();
        var sut = new SecretCleanupHostedService(scopeFactory, time, logger);

        await sut.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        await repo.Received(1).DeleteTerminalRowsAsync(utcNow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_logs_when_DeleteTerminalRowsAsync_returns_count_greater_than_zero()
    {
        var repo = Substitute.For<ISecretRepository>();
        repo.DeleteTerminalRowsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(3);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(new ServiceCollection().AddSingleton(repo).BuildServiceProvider());
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        var time = Substitute.For<ITimeProvider>();
        var utcNow = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        time.UtcNow.Returns(utcNow);
        var logger = Substitute.For<ILogger<SecretCleanupHostedService>>();
        using var cts = new CancellationTokenSource();
        var sut = new SecretCleanupHostedService(scopeFactory, time, logger);

        await sut.StartAsync(cts.Token);
        await Task.Delay(600);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("3") && o.ToString()!.Contains("terminal")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ExecuteAsync_catches_generic_exception_logs_and_continues_loop()
    {
        var repo = Substitute.For<ISecretRepository>();
        repo.DeleteTerminalRowsAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<int>(new InvalidOperationException("db unavailable")));
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(new ServiceCollection().AddSingleton(repo).BuildServiceProvider());
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        var time = Substitute.For<ITimeProvider>();
        time.UtcNow.Returns(DateTime.UtcNow);
        var logger = Substitute.For<ILogger<SecretCleanupHostedService>>();
        using var cts = new CancellationTokenSource();
        var sut = new SecretCleanupHostedService(scopeFactory, time, logger);

        await sut.StartAsync(cts.Token);
        await Task.Delay(600);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Is<Exception>(e => e is InvalidOperationException && e.Message.Contains("db unavailable")),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
