namespace SecureVault.Application.Common.Interfaces;

/// <summary>
/// Publish audit events (e.g. to RabbitMQ). Never include secret or plaintext.
/// </summary>
public interface IAuditPublisher
{
    Task PublishCreatedAsync(Guid secretId, string tokenIdHint, DateTime utcExpiresAt, CancellationToken cancellationToken = default);
    Task PublishRevealedAsync(Guid secretId, string tokenIdHint, CancellationToken cancellationToken = default);
}
