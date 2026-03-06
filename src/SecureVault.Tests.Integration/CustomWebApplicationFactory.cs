using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SecureVault.Api;

namespace SecureVault.Tests.Integration;

/// <summary>
/// Base WebApplicationFactory that configures the test host to use Testcontainers (Postgres, Redis, RabbitMQ)
/// instead of localhost or docker-compose service names. Override config BEFORE host build so no infra
/// connection to localhost:5432, localhost:6379, localhost:5672 is used.
/// </summary>
public abstract class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly string TestMasterKeyBase64 =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    protected readonly TestContainersFixture Fixture;

    protected CustomWebApplicationFactory(TestContainersFixture fixture)
    {
        Fixture = fixture;
        Environment.SetEnvironmentVariable("Encryption__MasterKeyBase64", TestMasterKeyBase64);
        // Set env vars so they are available when host config is built (before ConfigureAppConfiguration runs).
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", fixture.PostgresConnectionString);
        var redis = fixture.RedisConnectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", redis);
        Environment.SetEnvironmentVariable("Redis__ConnectionString", redis);
        Environment.SetEnvironmentVariable("Redis__Configuration", redis);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        // Add in-memory config; also set Redis via multiple keys so whichever the app reads is overridden.
        var redis = Fixture.RedisConnectionString;
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = Fixture.PostgresConnectionString,
                ["ConnectionStrings:Redis"] = redis,
                ["Redis:ConnectionString"] = redis,
                ["Redis:Configuration"] = redis,
                ["RabbitMQ:HostName"] = Fixture.RabbitMqHostName,
                ["RabbitMQ:Port"] = Fixture.RabbitMqPort.ToString(),
                ["RabbitMQ:UserName"] = Fixture.RabbitMqUserName,
                ["RabbitMQ:Password"] = Fixture.RabbitMqPassword,
                ["Encryption:MasterKeyBase64"] = TestMasterKeyBase64,
                ["SecureVault:BaseUrl"] = "http://localhost:3000",
                ["SecureVault:OverrideTtlSeconds"] = "2",
                ["RunMigrations"] = "true",
            });
        });
    }

    /// <summary>Exposed for integration tests that need to assert DB state (e.g. ciphertext/nonce at rest).</summary>
    public string GetPostgresConnectionString() => Fixture.PostgresConnectionString;
}
