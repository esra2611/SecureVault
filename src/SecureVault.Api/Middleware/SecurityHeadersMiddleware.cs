using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace SecureVault.Api.Middleware;

/// <summary>
/// Adds secure response headers and correlation ID for request tracing.
/// Sets Cache-Control no-store for secret routes; HSTS when not Development.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Correlation ID: from request or generate
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        context.TraceIdentifier = correlationId;

        // Secure headers (defense-in-depth)
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'";

        if (!_env.IsDevelopment())
            headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";

        // No caching for secret create/reveal (all responses: 200, 404, 410, 400, 429)
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/secrets", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/s/", StringComparison.Ordinal))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                context.Response.Headers["Pragma"] = "no-cache";
                context.Response.Headers["Expires"] = "0";
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }
}
