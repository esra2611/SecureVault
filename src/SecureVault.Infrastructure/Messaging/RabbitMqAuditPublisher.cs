using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SecureVault.Application.Common.Interfaces;

namespace SecureVault.Infrastructure.Messaging;

/// <summary>
/// Lazy connection: does not connect in constructor so app startup never fails due to RabbitMQ.
/// Audit is best-effort; connection/publish failures are logged and swallowed.
/// </summary>
public sealed class RabbitMqAuditPublisher : IAuditPublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqAuditPublisher> _logger;
    private IConnection? _connection;
    private readonly object _lock = new();
    private const string AuditRoutingKey = "audit.secret";

    public RabbitMqAuditPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqAuditPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private IConnection? GetConnection()
    {
        if (_connection is { IsOpen: true })
            return _connection;
        lock (_lock)
        {
            if (_connection is { IsOpen: true })
                return _connection;
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName ?? "guest",
                    Password = _options.Password ?? "guest"
                };
                _connection = factory.CreateConnection();
                return _connection;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ audit connection failed; audit events will be skipped.");
                return null;
            }
        }
    }

    public Task PublishCreatedAsync(Guid secretId, string tokenIdHint, DateTime utcExpiresAt, CancellationToken cancellationToken = default)
    {
        Publish("created", new { secretId, tokenIdHint, utcExpiresAt });
        return Task.CompletedTask;
    }

    public Task PublishRevealedAsync(Guid secretId, string tokenIdHint, CancellationToken cancellationToken = default)
    {
        Publish("revealed", new { secretId, tokenIdHint });
        return Task.CompletedTask;
    }

    private void Publish(string eventType, object payload)
    {
        var conn = GetConnection();
        if (conn is null)
            return;
        try
        {
            var messageId = Guid.NewGuid().ToString("N");
            using var channel = conn.CreateModel();
            var exchange = _options.Exchange ?? "securevault.audit";
            channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true);
            var envelope = new { eventType, payload, at = DateTime.UtcNow, messageId };
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = messageId;
            channel.BasicPublish(exchange, AuditRoutingKey, props, body);
            _logger.LogDebug("Audit published: {EventType} to {Exchange}/{RoutingKey}", eventType, exchange, AuditRoutingKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit publish failed for {EventType}.", eventType);
        }
    }

    public void Dispose() => _connection?.Dispose();
}

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? Exchange { get; set; }
}
