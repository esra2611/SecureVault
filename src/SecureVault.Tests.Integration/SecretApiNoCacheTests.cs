using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SecureVault.Tests.Integration;
using Xunit;

namespace SecureVault.Tests.Integration;

/// <summary>Tests with no-op cache to assert DB is source of truth; create/reveal/second 404 works without cache.</summary>
[Collection("Integration")]
public class SecretApiNoCacheTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly SecureVaultApiFixtureNoCache _fixture;

    public SecretApiNoCacheTests(TestContainersFixture containers)
    {
        _fixture = new SecureVaultApiFixtureNoCache(containers);
        _client = _fixture.CreateClient();
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Create_reveal_second_reveal_404_without_cache()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "no-cache-secret", expiry = "burn" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        var reveal1 = await _client.GetAsync($"/s/{token}");
        reveal1.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reveal1.Content.ReadFromJsonAsync<RevealSecretResponse>())!.Plaintext.Should().Be("no-cache-secret");

        var reveal2 = await _client.GetAsync($"/s/{token}");
        reveal2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record CreateSecretResponse(string ShareUrl, string TokenIdHint);
    private sealed record RevealSecretResponse(string Plaintext);
}
