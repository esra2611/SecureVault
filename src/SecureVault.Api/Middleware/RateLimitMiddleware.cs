using Microsoft.Extensions.Options;
using SecureVault.Application.Common.Interfaces;
using SecureVault.Infrastructure.RateLimiting;

namespace SecureVault.Api.Middleware;

/// <summary>
/// Applies rate limiting to create and reveal endpoints via IRateLimitService (Redis in production).
/// Returns 429 Too Many Requests when limit exceeded.
/// On backend failure: degrades open (allows request) to avoid 500 storms; logs warning.
/// Client ID: only uses X-Forwarded-For when RateLimiting:TrustProxy is true (behind trusted proxy).
/// </summary>
public sealed class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly RedisRateLimitOptions _options;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger, IOptions<RedisRateLimitOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, IRateLimitService rateLimit)
    {
        var (endpointKey, apply) = ResolveEndpoint(context.Request);
        if (!apply)
        {
            await _next(context);
            return;
        }

        var clientId = GetClientId(context, _options.TrustProxy);
        bool allowed;
        try
        {
            allowed = await rateLimit.TryAcquireAsync(endpointKey, clientId, context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rate limit check failed (Redis unavailable); allowing request (degrade-open).");
            allowed = true;
        }

        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsJsonAsync(new { message = "Too many requests. Please try again later." });
            return;
        }

        await _next(context);
    }

    private static (string endpointKey, bool apply) ResolveEndpoint(HttpRequest request)
    {
        var path = request.Path.Value ?? "";
        if (request.Method == "POST" && path.StartsWith("/api/secrets", StringComparison.OrdinalIgnoreCase))
            return ("create", true);
        if (request.Method == "GET" && path.StartsWith("/s/", StringComparison.Ordinal))
            return ("reveal", true);
        return ("", false);
    }

    private static string GetClientId(HttpContext context, bool trustProxy)
    {
        if (trustProxy)
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                var first = forwarded.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(first))
                    return first;
            }
        }
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
