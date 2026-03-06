using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Tests.Integration;

public sealed class NoOpAuditPublisher : IAuditPublisher
{
    public Task PublishCreatedAsync(Guid secretId, string tokenIdHint, DateTime utcExpiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishRevealedAsync(Guid secretId, string tokenIdHint, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
