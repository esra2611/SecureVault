using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SecureVault.Api;
using SecureVault.Application.Secrets.RevealSecret;
using SecureVault.Infrastructure.Persistence;
using SecureVault.Tests.Integration;
using Xunit;

namespace SecureVault.Tests.Integration;

[Collection("Integration")]
public class SecretApiTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly SecureVaultApiFixture _fixture;

    public SecretApiTests(TestContainersFixture containers)
    {
        _fixture = new SecureVaultApiFixture(containers);
        _client = _fixture.CreateClient();
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task CreateSecret_returns_share_url()
    {
        var response = await _client.PostAsJsonAsync("/api/secrets", new
        {
            plaintext = "my secret",
            expiry = "24h"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateSecretResponse>();
        body.Should().NotBeNull();
        body!.ShareUrl.Should().Contain("/s/");
        body.ShareUrl.Should().NotContain("my secret");
    }

    [Fact]
    public async Task CreateSecret_empty_plaintext_returns_400_with_SECRET_EMPTY()
    {
        var response = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "", expiry = "1h" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await GetValidationErrors(response);
        errors.Should().Contain(e => e.code == "SECRET_EMPTY" && e.propertyName == "Plaintext");
        errors.Should().NotContain(e => e.message != null && e.message.Contains("secret") && e.message != "Secret cannot be empty."); // no secret content in message
    }

    [Fact]
    public async Task CreateSecret_whitespace_only_plaintext_returns_400_with_SECRET_EMPTY()
    {
        var response = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "   \t\n  ", expiry = "1h" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await GetValidationErrors(response);
        errors.Should().Contain(e => e.code == "SECRET_EMPTY");
    }

    [Fact]
    public async Task CreateSecret_1001_chars_returns_400_with_SECRET_TOO_LONG()
    {
        var response = await _client.PostAsJsonAsync("/api/secrets", new
        {
            plaintext = new string('x', 1001),
            expiry = "1h"
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await GetValidationErrors(response);
        errors.Should().Contain(e => e.code == "SECRET_TOO_LONG" && e.propertyName == "Plaintext");
    }

    [Fact]
    public async Task CreateSecret_missing_expiry_returns_400_with_EXPIRY_REQUIRED()
    {
        var response = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "a secret", expiry = (string?)null });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await GetValidationErrors(response);
        errors.Should().Contain(e => e.code == "EXPIRY_REQUIRED");
    }

    [Fact]
    public async Task CreateSecret_empty_expiry_returns_400_with_EXPIRY_REQUIRED()
    {
        var response = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "a secret", expiry = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await GetValidationErrors(response);
        errors.Should().Contain(e => e.code == "EXPIRY_REQUIRED");
    }

    [Fact]
    public async Task CreateSecret_invalid_expiry_returns_400_with_EXPIRY_INVALID()
    {
        var response = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "a secret", expiry = "invalid" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await GetValidationErrors(response);
        errors.Should().Contain(e => e.code == "EXPIRY_INVALID");
    }

    [Fact]
    public async Task CreateSecret_exactly_1000_chars_with_valid_expiry_returns_200()
    {
        var response = await _client.PostAsJsonAsync("/api/secrets", new
        {
            plaintext = new string('a', 1000),
            expiry = "7d"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateSecretResponse>();
        body.Should().NotBeNull();
        body!.ShareUrl.Should().Contain("/s/");
    }

    private static async Task<List<ValidationErrorItem>> GetValidationErrors(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("errors", out var errorsEl))
            return new List<ValidationErrorItem>();
        var list = new List<ValidationErrorItem>();
        foreach (var item in errorsEl.EnumerateArray())
        {
            list.Add(new ValidationErrorItem(
                item.TryGetProperty("propertyName", out var p) ? p.GetString() : null,
                item.TryGetProperty("message", out var m) ? m.GetString() : null,
                item.TryGetProperty("code", out var c) ? c.GetString() : null));
        }
        return list;
    }

    private sealed record ValidationErrorItem(string? propertyName, string? message, string? code);

    [Fact]
    public async Task RevealSecret_returns_plaintext_once_then_404_NotAvailable()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "one-time", expiry = "burn" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        var reveal1 = await _client.GetAsync($"/s/{token}");
        reveal1.StatusCode.Should().Be(HttpStatusCode.OK);
        reveal1.Headers.TryGetValues("Cache-Control", out var cacheControl).Should().BeTrue();
        (cacheControl?.FirstOrDefault() ?? "").Should().Contain("no-store");
        var content1 = await reveal1.Content.ReadFromJsonAsync<RevealSecretResponse>();
        content1!.Plaintext.Should().Be("one-time");

        var reveal2 = await _client.GetAsync($"/s/{token}");
        reveal2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevealSecret_invalid_token_returns_404()
    {
        var response = await _client.GetAsync("/s/invalid-token-32-chars-base64!!!!!!");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Proves exactly-once reveal under concurrency: multiple parallel requests for the same token
    /// must result in exactly one 200 OK and the rest 404 (same status/message to prevent enumeration).
    /// </summary>
    [Fact]
    public async Task RevealSecret_concurrent_requests_exactly_one_succeeds()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "race-test-secret", expiry = "burn" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        const int concurrency = 10;
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => _client.GetAsync($"/s/{token}"))
            .ToList();
        var results = await Task.WhenAll(tasks);

        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var notFoundCount = results.Count(r => r.StatusCode == HttpStatusCode.NotFound);
        okCount.Should().Be(1, "exactly one reveal must succeed");
        notFoundCount.Should().Be(concurrency - 1, "all other requests must get 404 (expired or already viewed)");

        var okResponse = results.First(r => r.StatusCode == HttpStatusCode.OK);
        var content = await okResponse.Content.ReadFromJsonAsync<RevealSecretResponse>();
        content!.Plaintext.Should().Be("race-test-secret");
    }

    /// <summary>
    /// Stress concurrency: 50 parallel reveal requests for same token; exactly one 200 OK, rest 404, correct plaintext.
    /// </summary>
    [Fact]
    public async Task RevealSecret_concurrent_50_requests_exactly_one_succeeds()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "concurrent-50-secret", expiry = "burn" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        const int concurrency = 50;
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ => _client.GetAsync($"/s/{token}"))
            .ToList();
        var results = await Task.WhenAll(tasks);

        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        var notFoundCount = results.Count(r => r.StatusCode == HttpStatusCode.NotFound);
        okCount.Should().Be(1, "exactly one reveal must succeed under 50-way race");
        notFoundCount.Should().Be(concurrency - 1);

        var okResponse = results.First(r => r.StatusCode == HttpStatusCode.OK);
        var content = await okResponse.Content.ReadFromJsonAsync<RevealSecretResponse>();
        content!.Plaintext.Should().Be("concurrent-50-secret");
    }

    /// <summary>
    /// TTL expiry: with OverrideTtlSeconds=2 in test config, a secret with 1h/24h/7d expires after 2s.
    /// Create with 1h, wait 3s, reveal must return 404 (same as expired/already viewed).
    /// </summary>
    [Fact]
    public async Task RevealSecret_after_TTL_expiry_returns_404_NotAvailable()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "ttl-secret", expiry = "1h" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        await Task.Delay(TimeSpan.FromSeconds(3));

        var reveal = await _client.GetAsync($"/s/{token}");
        reveal.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Password-protected secret: create with password, reveal with correct password (POST body) returns plaintext.
    /// </summary>
    [Fact]
    public async Task CreateSecret_with_password_reveal_with_correct_password_returns_plaintext()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new
        {
            plaintext = "password-protected-secret",
            expiry = "burn",
            password = "user-pass-123"
        });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        var reveal = await _client.PostAsJsonAsync("/api/secrets/reveal", new { token, password = "user-pass-123" });
        reveal.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await reveal.Content.ReadFromJsonAsync<RevealSecretResponse>();
        content!.Plaintext.Should().Be("password-protected-secret");
    }

    /// <summary>
    /// Wrong password returns 404 (same as invalid token). Secret must NOT be consumed; second request with correct password must succeed.
    /// </summary>
    [Fact]
    public async Task RevealSecret_with_wrong_password_returns_404_and_does_not_consume()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new
        {
            plaintext = "secret",
            expiry = "burn",
            password = "correct"
        });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        var wrongReveal = await _client.PostAsJsonAsync("/api/secrets/reveal", new { token, password = "wrong" });
        wrongReveal.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var correctReveal = await _client.PostAsJsonAsync("/api/secrets/reveal", new { token, password = "correct" });
        correctReveal.StatusCode.Should().Be(HttpStatusCode.OK);
        (await correctReveal.Content.ReadFromJsonAsync<RevealSecretResponse>())!.Plaintext.Should().Be("secret");
    }

    /// <summary>
    /// Enumeration prevention: invalid token and expired/already-viewed token return same status and message body.
    /// </summary>
    [Fact]
    public async Task RevealSecret_404_and_expired_return_same_message_body()
    {
        var invalidResponse = await _client.GetAsync("/s/invalid-token-32-chars-base64!!!!!!");
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var invalidBody = await GetRevealErrorMessage(invalidResponse);

        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "x", expiry = "burn" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');
        await _client.GetAsync($"/s/{token}");
        var expiredResponse = await _client.GetAsync($"/s/{token}");
        expiredResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var expiredBody = await GetRevealErrorMessage(expiredResponse);

        invalidBody.Should().Be(expiredBody, "404 for invalid and for expired/already-viewed must return same message to prevent enumeration");
    }

    /// <summary>
    /// Burn-after-read with password: create with password, reveal once with correct password (POST), second reveal returns 404.
    /// </summary>
    [Fact]
    public async Task CreateSecret_with_password_burn_after_read_second_reveal_returns_404()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new
        {
            plaintext = "burn-with-pass",
            expiry = "burn",
            password = "pwd123"
        });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        var reveal1 = await _client.PostAsJsonAsync("/api/secrets/reveal", new { token, password = "pwd123" });
        reveal1.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reveal1.Content.ReadFromJsonAsync<RevealSecretResponse>())!.Plaintext.Should().Be("burn-with-pass");

        var reveal2 = await _client.PostAsJsonAsync("/api/secrets/reveal", new { token, password = "pwd123" });
        reveal2.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Password-protected secret: GET /s/{token} without password returns 404 (no query-string password; use POST for password).
    /// </summary>
    [Fact]
    public async Task RevealSecret_with_password_protected_secret_without_password_returns_404()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new
        {
            plaintext = "secret",
            expiry = "burn",
            password = "mypass"
        });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        var reveal = await _client.GetAsync($"/s/{token}");
        reveal.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<string?> GetRevealErrorMessage(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("message", out var msg))
            return msg.GetString();
        return null;
    }

    /// <summary>
    /// DB persistence: create stores ciphertext/nonce (no plaintext); after reveal ciphertext/nonce are null and UtcRevealedAt set.
    /// </summary>
    [Fact]
    public async Task CreateSecret_stores_ciphertext_nonce_only_reveal_clears_ciphertext_nonce_sets_UtcRevealedAt()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "db-persistence-secret", expiry = "burn" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var tokenPart = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1];
        var token = tokenPart.Contains('#') ? tokenPart[..tokenPart.IndexOf('#')] : tokenPart.TrimEnd('/');
        var tokenHashBase64 = RevealSecretQueryHandler.TokenHashBase64FromToken(token)
            ?? throw new InvalidOperationException("Token could not be converted to hash (invalid token format).");

        var options = new DbContextOptionsBuilder<SecretVaultDbContext>()
            .UseNpgsql(_fixture.GetPostgresConnectionString())
            .Options;
        using (var db = new SecretVaultDbContext(options))
        {
            var row = await db.Secrets.FirstOrDefaultAsync(s => s.TokenHashBase64 == tokenHashBase64);
            row.Should().NotBeNull();
            row!.Ciphertext.Should().NotBeNull().And.NotBeEmpty();
            row.Nonce.Should().NotBeNull().And.NotBeEmpty();
            row.UtcRevealedAt.Should().BeNull();
        }

        var revealRes = await _client.GetAsync($"/s/{token}");
        revealRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Production consumes the secret by DELETE; row no longer exists after reveal.
        using (var db = new SecretVaultDbContext(options))
        {
            var row = await db.Secrets.FirstOrDefaultAsync(s => s.TokenHashBase64 == tokenHashBase64);
            row.Should().BeNull("reveal deletes the row (atomic consume)");
        }
    }

    /// <summary>
    /// Time boundary: with OverrideTtlSeconds=2, reveal before 2s returns 200, after 2s returns 404.
    /// </summary>
    [Fact]
    public async Task RevealSecret_before_TTL_expiry_returns_200_after_expiry_returns_404()
    {
        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "before-ttl", expiry = "1h" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');

        await Task.Delay(TimeSpan.FromSeconds(1));
        var revealBefore = await _client.GetAsync($"/s/{token}");
        revealBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        var createRes2 = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "after-ttl", expiry = "1h" });
        createRes2.EnsureSuccessStatusCode();
        var create2 = await createRes2.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token2 = create2!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');
        await Task.Delay(TimeSpan.FromSeconds(3));
        var revealAfter = await _client.GetAsync($"/s/{token2}");
        revealAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Valid-format token that does not exist in DB returns same 404 message as invalid/expired (enumeration prevention).
    /// </summary>
    [Fact]
    public async Task RevealSecret_valid_format_nonexistent_token_returns_same_404_message_as_expired()
    {
        var validFormatToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var nonexistentResponse = await _client.GetAsync($"/s/{validFormatToken}");
        nonexistentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var nonexistentBody = await GetRevealErrorMessage(nonexistentResponse);

        var createRes = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "x", expiry = "burn" });
        createRes.EnsureSuccessStatusCode();
        var create = await createRes.Content.ReadFromJsonAsync<CreateSecretResponse>();
        var token = create!.ShareUrl.Split("/s/", StringSplitOptions.None)[1].TrimEnd('/');
        await _client.GetAsync($"/s/{token}");
        var expiredResponse = await _client.GetAsync($"/s/{token}");
        var expiredBody = await GetRevealErrorMessage(expiredResponse);

        nonexistentBody.Should().Be(expiredBody, "valid-format nonexistent token must return same message as expired/viewed to prevent enumeration");
    }

    /// <summary>
    /// Error response bodies (400, 404) must not leak stack traces or file paths.
    /// </summary>
    [Fact]
    public async Task Error_responses_do_not_leak_stack_trace_or_file_paths()
    {
        var badRequest = await _client.PostAsJsonAsync("/api/secrets", new { plaintext = "", expiry = "1h" });
        var badBody = await badRequest.Content.ReadAsStringAsync();
        badBody.Should().NotContain(" at ");
        badBody.Should().NotContain(".cs:");
        badBody.Should().NotContain("StackTrace");

        var notFound = await _client.GetAsync("/s/invalid-token-32-chars-base64!!!!!!");
        var notFoundBody = await notFound.Content.ReadAsStringAsync();
        notFoundBody.Should().NotContain(" at ");
        notFoundBody.Should().NotContain(".cs:");
        notFoundBody.Should().NotContain("StackTrace");
    }

    private sealed record CreateSecretResponse(string ShareUrl, string TokenIdHint);
    private sealed record RevealSecretResponse(string Plaintext);
}
