namespace SecureVault.Application.Secrets.RevealSecret;

/// <summary>Outcome of reveal for HTTP mapping: 200 OK, 410 Gone (expired/already viewed), or 404 Not Found (invalid token).</summary>
public abstract record RevealSecretHandlerOutcome;

public sealed record RevealSecretSuccessOutcome(RevealSecretResult Result) : RevealSecretHandlerOutcome;

public sealed record RevealSecretExpiredOutcome : RevealSecretHandlerOutcome;

public sealed record RevealSecretNotFoundOutcome : RevealSecretHandlerOutcome;
