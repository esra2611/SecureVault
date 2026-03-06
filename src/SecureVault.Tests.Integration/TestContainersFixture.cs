using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace SecureVault.Tests.Integration;

/// <summary>
/// Shared fixture that starts PostgreSQL, Redis, and RabbitMQ containers once per test run.
/// Use as a collection fixture so containers start once and all integration tests reuse them.
/// </summary>
public sealed class TestContainersFixture : Xunit.IAsyncLifetime, IDisposable
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("securevault")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management-alpine")
        .Build();

    public string PostgresConnectionString { get; private set; } = "";
    public string RedisConnectionString { get; private set; } = "";
    public string RedisEndpoint => RedisConnectionString; // alias for clarity
    public string RabbitMqHostName => "localhost";
    public int RabbitMqPort { get; private set; }
    public string RabbitMqUserName { get; } = "guest";
    public string RabbitMqPassword { get; } = "guest";
    /// <summary>AMQP connection string for RabbitMQ (e.g. amqp://guest:guest@localhost:port).</summary>
    public string RabbitMqConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _redis.StartAsync(),
            _rabbitMq.StartAsync()).ConfigureAwait(false);

        PostgresConnectionString = _postgres.GetConnectionString();
        RedisConnectionString = _redis.GetConnectionString();
        RabbitMqPort = _rabbitMq.GetMappedPublicPort(5672);
        RabbitMqConnectionString = _rabbitMq.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
        await _redis.DisposeAsync().ConfigureAwait(false);
        await _rabbitMq.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }
}
