using System.Net;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SecureVault.Application.Secrets.CreateSecret;
using SecureVault.Application.Secrets.RevealSecret;
using SecureVault.Domain.ValueObjects;

namespace SecureVault.Api.Endpoints;

public static class SecretEndpoints
{
    private const string ExpiredOrInvalidMessage = "This secret has expired or has already been viewed.";

    /// <summary>Allowed expiry values from client (required; no default).</summary>
    private static readonly HashSet<string> AllowedExpiryValues = new(StringComparer.OrdinalIgnoreCase)
        { "burn", "burn_after_read", "1h", "1_hour", "24h", "24_hours", "7d", "7_days" };

    public static void MapSecretEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/secrets").WithTags("Secrets");

        group.MapPost("", CreateSecret)
            .WithName("CreateSecret")
            .WithSummary("Create a secret")
            .WithDescription("Creates a time-limited secret and returns a shareable link. Optional 'password' enables password protection; the same password is required to reveal the secret.")
            .Produces<CreateSecretResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AllowAnonymous();

        group.MapPost("reveal", RevealSecretPost)
            .WithName("RevealSecretPost")
            .WithSummary("Reveal a secret once (with password in body)")
            .WithDescription("Use this when the secret is password-protected; password in body avoids query-string encoding issues (e.g. +). Same response as GET /s/{token}.")
            .Produces<RevealSecretResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();
        // Shareable link: GET /s/{token} (mapped at root below)
    }

    /// <summary>Map the public shareable link route: /s/{token}</summary>
    /// <remarks>Invalid, expired, or already-viewed tokens all return 404 with the same message to prevent enumeration.</remarks>
    public static void MapShareableLinkEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/s/{token:required}", RevealSecret)
            .WithName("RevealSecret")
            .WithSummary("Reveal a secret once")
            .WithDescription("Returns the secret plaintext once after consumption. For password-protected secrets use POST /api/secrets/reveal with password in body. Invalid token, expired, already viewed, or wrong password all return 404 with the same message to prevent enumeration.")
            .Produces<RevealSecretResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();
    }

    private static async Task<IResult> CreateSecret(
        [FromBody] CreateSecretRequest request,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        // Expiry: required, only [burn, 1h, 24h, 7d] (and aliases)
        var expiryRaw = request.Expiry?.Trim();
        if (string.IsNullOrEmpty(expiryRaw))
        {
            return ValidationErrorResult(HttpStatusCode.BadRequest, "Expiry", "Expiry is required.", CreateSecretCommandValidator.CodeExpiryRequired);
        }
        if (!AllowedExpiryValues.Contains(expiryRaw!))
        {
            return ValidationErrorResult(HttpStatusCode.BadRequest, "Expiry", "Expiry must be one of: burn, 1h, 24h, 7d.", CreateSecretCommandValidator.CodeExpiryInvalid);
        }
        var expiryType = expiryRaw!.ToLowerInvariant() switch
        {
            "burn" or "burn_after_read" => ExpiryType.BurnAfterRead,
            "1h" or "1_hour" => ExpiryType.OneHour,
            "24h" or "24_hours" => ExpiryType.TwentyFourHours,
            "7d" or "7_days" => ExpiryType.SevenDays,
            _ => ExpiryType.TwentyFourHours
        };

        var plaintext = (request.Plaintext ?? string.Empty).Trim();
        var password = request.Password?.Trim();
        var command = new CreateSecretCommand(plaintext, expiryType, password);
        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return Results.Ok(new CreateSecretResponse(result.ShareUrl, result.TokenIdHint));
        }
        catch (FluentValidation.ValidationException ex)
        {
            return ToProblemDetails(ex);
        }
    }

    /// <summary>RFC 7807 ProblemDetails with error codes; validation errors -> 400.</summary>
    private static IResult ToProblemDetails(FluentValidation.ValidationException ex)
    {
        var errors = ex.Errors
            .Select(f => new { propertyName = f.PropertyName, message = f.ErrorMessage, code = f.ErrorCode ?? "VALIDATION_ERROR" })
            .ToList();
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more validation errors occurred."
        };
        problem.Extensions["errors"] = errors;
        return Results.Json(problem, statusCode: StatusCodes.Status400BadRequest, options: null);
    }

    private static IResult ValidationErrorResult(HttpStatusCode status, string propertyName, string message, string code)
    {
        var errors = new[] { new { propertyName, message, code } };
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = "Validation failed",
            Status = (int)status,
            Detail = message
        };
        problem.Extensions["errors"] = errors;
        return Results.Json(problem, statusCode: (int)status, options: null);
    }

    private static async Task<IResult> RevealSecret(
        HttpContext context,
        string token,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        return await MapRevealOutcome(context, mediator, token, null, cancellationToken);
    }

    private static async Task<IResult> RevealSecretPost(
        HttpContext context,
        [FromBody] RevealSecretRequest? body,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var token = body?.Token?.Trim();
        var passwordTrimmed = body?.Password?.Trim();
        return await MapRevealOutcome(context, mediator, token ?? "", passwordTrimmed, cancellationToken);
    }

    private static async Task<IResult> MapRevealOutcome(
        HttpContext context,
        IMediator mediator,
        string token,
        string? passwordTrimmed,
        CancellationToken cancellationToken)
    {
        var outcome = await mediator.Send(new RevealSecretQuery(token, passwordTrimmed), cancellationToken);
        return outcome switch
        {
            RevealSecretSuccessOutcome success => OkWithNoCache(context, new RevealSecretResponse(success.Result.Plaintext)),
            RevealSecretExpiredOutcome => Results.Json(new { message = ExpiredOrInvalidMessage }, statusCode: StatusCodes.Status404NotFound),
            _ => Results.Json(new { message = ExpiredOrInvalidMessage }, statusCode: StatusCodes.Status404NotFound)
        };
    }

    private static IResult OkWithNoCache(HttpContext context, RevealSecretResponse response)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
        return Results.Ok(response);
    }
}

public sealed record CreateSecretRequest(string? Plaintext, string? Expiry, string? Password);
public sealed record CreateSecretResponse(string ShareUrl, string TokenIdHint);

public sealed record RevealSecretRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("token")] string? Token,
    [property: System.Text.Json.Serialization.JsonPropertyName("password")] string? Password);

public sealed record RevealSecretResponse(string Plaintext);
