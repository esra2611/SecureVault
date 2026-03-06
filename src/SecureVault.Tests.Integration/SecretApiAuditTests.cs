using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SecureVault.Tests.Integration;
using Xunit;

namespace SecureVault.Tests.Integration;

/// <summary>Tests that audit publisher is called on successful reveal.</summary>
[Collection("Integration")]
public class SecretApiAuditTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly SecureVaultApiFixtureWithAuditSpy _fixture;

    public SecretApiAuditTests(TestContainersFixture containers)
    {
        _fixture = new SecureVaultApiFixtureWithAuditSpy(containers);
        _client = _fixture.CreateClient();
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task RevealSecret_success_calls_PublishRevealedAsync_once()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "audit-secret", expiry = "burn" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        _fixture.AuditSpy.RevealedCalls.Should().BeEmpty();

        var reveal = await _client.GetAsync($"/s/{token}");
        reveal.StatusCode.Should().Be(HttpStatusCode.OK);

        _fixture.AuditSpy.RevealedCalls.Should().HaveCount(1);
    }

    private sealed record CreateSecretResponse(string ShareUrl, string TokenIdHint);
}
