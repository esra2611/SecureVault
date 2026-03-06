using Xunit;

namespace SecureVault.Tests.Integration;

/// <summary>
/// Collection definition for integration tests. Uses TestContainersFixture so Postgres, Redis, and RabbitMQ
/// containers start once and are shared by all test classes in this collection.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<TestContainersFixture>
{
}
