using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SecureVault.Infrastructure.Messaging;
using Xunit;

namespace SecureVault.Tests.Unit;

public sealed class RabbitMqAuditPublisherTests
{
    [Fact]
    public void Constructor_accepts_options_and_logger()
    {
        var options = Options.Create(new RabbitMqOptions { HostName = "localhost", Port = 5672 });
        var logger = Substitute.For<ILogger<RabbitMqAuditPublisher>>();

        var act = () => new RabbitMqAuditPublisher(options, logger);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task PublishCreatedAsync_returns_completed_task_and_does_not_throw()
    {
        var options = Options.Create(new RabbitMqOptions { HostName = "localhost", Port = 5672 });
        var logger = Substitute.For<ILogger<RabbitMqAuditPublisher>>();
        using var sut = new RabbitMqAuditPublisher(options, logger);

        var act = () => sut.PublishCreatedAsync(Guid.NewGuid(), "hint", DateTime.UtcNow, CancellationToken.None);

        await act.Should().NotThrowAsync();
        var task = sut.PublishCreatedAsync(Guid.NewGuid(), "hint", DateTime.UtcNow, CancellationToken.None);
        await task;
        task.Status.Should().Be(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task PublishRevealedAsync_returns_completed_task_and_does_not_throw()
    {
        var options = Options.Create(new RabbitMqOptions { HostName = "localhost", Port = 5672 });
        var logger = Substitute.For<ILogger<RabbitMqAuditPublisher>>();
        using var sut = new RabbitMqAuditPublisher(options, logger);

        var act = () => sut.PublishRevealedAsync(Guid.NewGuid(), "hint", CancellationToken.None);

        await act.Should().NotThrowAsync();
        var task = sut.PublishRevealedAsync(Guid.NewGuid(), "hint", CancellationToken.None);
        await task;
        task.Status.Should().Be(TaskStatus.RanToCompletion);
    }
}
