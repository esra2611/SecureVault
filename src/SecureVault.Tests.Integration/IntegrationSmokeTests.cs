using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace SecureVault.Tests.Integration;

/// <summary>
/// Smoke tests to ensure integration tests use Testcontainers endpoints, not localhost default ports
/// or docker-compose service names (postgres, redis, rabbitmq).
/// </summary>
[Collection("Integration")]
public sealed class IntegrationSmokeTests
{
    private readonly TestContainersFixture _fixture;

    public IntegrationSmokeTests(TestContainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Fixture_exposes_container_endpoints_not_localhost_or_compose_names()
    {
        _fixture.PostgresConnectionString.Should().NotBeNullOrWhiteSpace(
            "Postgres connection string must be set by Testcontainers");
        _fixture.PostgresConnectionString.Should().NotContain("Host=postgres",
            "must not use docker-compose service name 'postgres'");
        _fixture.PostgresConnectionString.Should().NotContain("Host=localhost;Port=5432;",
            "must not use default localhost:5432 when docker is down");

        _fixture.RedisConnectionString.Should().NotBeNullOrWhiteSpace(
            "Redis connection string must be set by Testcontainers");
        _fixture.RedisConnectionString.Should().NotBe("localhost:6379",
            "must not use default localhost:6379 when docker is down");
        _fixture.RedisConnectionString.Should().NotContain("redis:6379",
            "must not use docker-compose service name 'redis'");

        _fixture.RabbitMqPort.Should().BeGreaterThan(0,
            "RabbitMQ port must be set by Testcontainers");
        _fixture.RabbitMqConnectionString.Should().NotBeNullOrWhiteSpace(
            "RabbitMQ connection string must be set");
        _fixture.RabbitMqConnectionString.Should().NotContain("rabbitmq:5672",
            "must not use docker-compose service name 'rabbitmq' with default port");
    }

    /// <summary>
    /// Asserts the API actually uses container Redis (from test-config endpoint), not localhost:6379.
    /// </summary>
    [Fact]
    public async Task API_uses_container_Redis_not_localhost_6379()
    {
        using var factory = new SecureVaultApiFixture(_fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/test-config/redis");
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "IntegrationTests env must expose /test-config/redis");

        var body = await response.Content.ReadFromJsonAsync<TestConfigRedisResponse>();
        body.Should().NotBeNull();
        body!.Redis.Should().NotBeNullOrWhiteSpace("API must have Redis config");
        body.Redis.Should().NotBe("localhost:6379",
            "API must use Testcontainers Redis, not default localhost:6379");
        body.Redis.Should().NotContain("redis:6379",
            "API must not use docker-compose service name 'redis'");
    }

    private sealed record TestConfigRedisResponse([property: JsonPropertyName("redis")] string Redis);
}
