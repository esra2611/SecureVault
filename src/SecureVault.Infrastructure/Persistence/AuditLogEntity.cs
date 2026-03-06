namespace SecureVault.Infrastructure.Persistence;

/// <summary>
/// Persisted audit event (consumed from RabbitMQ). No secret or plaintext—metadata only.
/// MessageId: dedup key for idempotent consumption (RabbitMQ at-least-once).
/// </summary>
public sealed class AuditLogEntity
{
    public long Id { get; set; }
    /// <summary>Unique id from publisher (AMQP MessageId); null for legacy messages.</summary>
    public string? MessageId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
