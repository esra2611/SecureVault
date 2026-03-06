using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Tests.Integration;

/// <summary>Records audit calls for integration tests (e.g. assert PublishRevealedAsync called on success).</summary>
public sealed class SpyAuditPublisher : IAuditPublisher
{
    public IReadOnlyList<(Guid SecretId, string TokenIdHint, DateTime UtcExpiresAt)> CreatedCalls => _created.ToList();
    public IReadOnlyList<(Guid SecretId, string TokenIdHint)> RevealedCalls => _revealed.ToList();

    private readonly List<(Guid, string, DateTime)> _created = new();
    private readonly List<(Guid, string)> _revealed = new();

    public Task PublishCreatedAsync(Guid secretId, string tokenIdHint, DateTime utcExpiresAt, CancellationToken cancellationToken = default)
    {
        _created.Add((secretId, tokenIdHint, utcExpiresAt));
        return Task.CompletedTask;
    }

    public Task PublishRevealedAsync(Guid secretId, string tokenIdHint, CancellationToken cancellationToken = default)
    {
        _revealed.Add((secretId, tokenIdHint));
        return Task.CompletedTask;
    }
}
