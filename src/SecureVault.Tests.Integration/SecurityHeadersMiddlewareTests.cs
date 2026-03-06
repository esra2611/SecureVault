using System.Net;
using FluentAssertions;
using Xunit;

namespace SecureVault.Tests.Integration;

/// <summary>
/// Integration tests for SecurityHeadersMiddleware. Verifies that the middleware runs in the pipeline
/// and adds the expected security headers to responses (e.g. X-Correlation-ID, X-Content-Type-Options, etc.).
/// </summary>
[Collection("Integration")]
public sealed class SecurityHeadersMiddlewareTests
{
    private readonly TestContainersFixture _fixture;

    public SecurityHeadersMiddlewareTests(TestContainersFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_health_returns_success_and_response_includes_security_headers_from_middleware()
    {
        using var factory = new SecureVaultApiFixture(_fixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "pipeline should run and /health should return 200");
        response.Headers.Should().Contain(h => h.Key == "X-Correlation-ID",
            "middleware sets correlation ID for request tracing");
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Should().Contain("default-src 'self'; frame-ancestors 'none'; base-uri 'self'");
    }
}
