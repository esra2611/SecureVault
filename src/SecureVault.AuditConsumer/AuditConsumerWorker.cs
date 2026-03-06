using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace SecureVault.AuditConsumer;

/// <summary>
/// Consumes audit events from RabbitMQ and persists them to PostgreSQL AuditLogs table.
/// Queue is durable; messages are acknowledged only after successful write.
/// On graceful shutdown: cancels consumer, drains in-flight messages (up to DrainTimeoutSeconds), then exits.
/// </summary>
public sealed class AuditConsumerWorker : BackgroundService
{
    private const string AuditRoutingKey = "audit.secret";
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(30);
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly string _connectionString;
    private readonly ILogger<AuditConsumerWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private IConnection? _connection;
    private IModel? _channel;
    private string? _consumerTag;
    private int _inFlightCount;
    private readonly object _consumerLock = new();

    public AuditConsumerWorker(
        IOptions<RabbitMqOptions> rabbitOptions,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        ILogger<AuditConsumerWorker> logger)
    {
        _rabbitOptions = rabbitOptions.Value;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        _lifetime = lifetime;
        _logger = logger;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Audit consumer stopping; cancelling consumer and draining in-flight (max {Seconds}s).", DrainTimeout.TotalSeconds);
        string? tag;
        lock (_consumerLock)
        {
            tag = _consumerTag;
            if (_channel?.IsOpen == true && !string.IsNullOrEmpty(tag))
            {
                try
                {
                    _channel.BasicCancel(tag);
                    _consumerTag = null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "BasicCancel failed.");
                }
            }
        }
        var deadline = DateTime.UtcNow.Add(DrainTimeout);
        while (Volatile.Read(ref _inFlightCount) > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, cancellationToken);
        }
        if (Volatile.Read(ref _inFlightCount) > 0)
            _logger.LogWarning("Drain timeout; {Count} message(s) still in flight.", Volatile.Read(ref _inFlightCount));
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EnsureConnected();
                if (_channel is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += async (_, ea) => await OnMessageReceivedAsync(ea);
                lock (_consumerLock)
                {
                    _consumerTag = _channel.BasicConsume(
                        queue: _rabbitOptions.QueueName,
                        autoAck: false,
                        consumer: consumer);
                }

                _logger.LogInformation("Audit consumer started. Queue: {Queue}", _rabbitOptions.QueueName);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit consumer error; reconnecting in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void EnsureConnected()
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            return;

        _channel?.Dispose();
        _connection?.Dispose();

        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.HostName,
            Port = _rabbitOptions.Port,
            UserName = _rabbitOptions.UserName ?? "guest",
            Password = _rabbitOptions.Password ?? "guest",
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(_rabbitOptions.Exchange, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(
            _rabbitOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);
        _channel.QueueBind(_rabbitOptions.QueueName, _rabbitOptions.Exchange, AuditRoutingKey);
    }

    private async Task OnMessageReceivedAsync(BasicDeliverEventArgs ea)
    {
        if (_channel is null)
            return;
        Interlocked.Increment(ref _inFlightCount);
        try
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var messageId = ea.BasicProperties?.MessageId
                ?? (root.TryGetProperty("messageId", out var mid) ? mid.GetString() : null);
            var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "unknown" : "unknown";
            var payloadJson = root.TryGetProperty("payload", out var p) ? p.GetRawText() : "{}";
            var occurredAt = root.TryGetProperty("at", out var at) && at.TryGetDateTime(out var dt)
                ? dt
                : DateTime.UtcNow;

            // When MessageId is missing (legacy or redelivery), derive idempotency key from payload so duplicates are deduped
            var idempotencyKey = messageId ?? DeriveIdempotencyKey(eventType, payloadJson, occurredAt);

            await PersistAuditAsync(idempotencyKey, eventType, payloadJson, occurredAt);

            _channel.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process audit message; nacking (no requeue, send to DLQ if configured).");
            _channel.BasicNack(ea.DeliveryTag, false, requeue: false);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlightCount);
        }
    }

    private static string? DeriveIdempotencyKey(string eventType, string payloadJson, DateTime occurredAtUtc)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("secretId", out var sid))
                return null;
            var secretId = sid.GetGuid().ToString("N");
            var composite = $"{secretId}|{eventType}|{occurredAtUtc:O}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(composite));
            return Convert.ToHexString(hash).ToLowerInvariant()[..64];
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistAuditAsync(string? idempotencyKey, string eventType, string payloadJson, DateTime occurredAtUtc)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            // Idempotent: duplicate message (same MessageId / derived key) does not insert again
            await using var cmd = new Npgsql.NpgsqlCommand(
                """
                INSERT INTO "AuditLogs" ("MessageId", "EventType", "PayloadJson", "OccurredAtUtc", "CreatedAtUtc")
                VALUES (:messageId, :eventType, :payloadJson, :occurredAtUtc, :createdAtUtc)
                ON CONFLICT ("MessageId") WHERE "MessageId" IS NOT NULL DO NOTHING
                """,
                conn);
            cmd.Parameters.AddWithValue("messageId", idempotencyKey);
            cmd.Parameters.AddWithValue("eventType", eventType);
            cmd.Parameters.AddWithValue("payloadJson", payloadJson);
            cmd.Parameters.AddWithValue("occurredAtUtc", occurredAtUtc);
            cmd.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // No idempotency key (payload missing secretId or invalid): insert only, duplicates possible
            await using var cmd = new Npgsql.NpgsqlCommand(
                """
                INSERT INTO "AuditLogs" ("EventType", "PayloadJson", "OccurredAtUtc", "CreatedAtUtc")
                VALUES (:eventType, :payloadJson, :occurredAtUtc, :createdAtUtc)
                """,
                conn);
            cmd.Parameters.AddWithValue("eventType", eventType);
            cmd.Parameters.AddWithValue("payloadJson", payloadJson);
            cmd.Parameters.AddWithValue("occurredAtUtc", occurredAtUtc);
            cmd.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        _logger.LogDebug("Audit persisted: {EventType} at {OccurredAt}", eventType, occurredAtUtc);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
